using System.Diagnostics.CodeAnalysis;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DuplicateDetectionRepositoryTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task EnqueueAsync_PersistsPendingItem()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            Assert.IsGreaterThan(0, queueId);
            Assert.AreEqual((int)DuplicateDetectionStatus.Pending, status);
            Assert.AreEqual(string.Empty, lastError);
        }

        [TestMethod]
        public async Task EnqueueAsync_ReturnsZeroWhenActiveItemAlreadyExists()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long first = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            long second = await database.EnqueueAsync(findingId, TestContext.CancellationToken);

            Assert.IsGreaterThan(0, first);
            Assert.AreEqual(0L, second);
        }

        [TestMethod]
        public async Task EnqueueAsync_AllowsNewItemAfterPreviousCompleted()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long first = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            _ = await database.Queue.CompleteAsync(claimed.Id, claimed.AttemptCount, TestContext.CancellationToken);

            long second = await database.EnqueueAsync(findingId, TestContext.CancellationToken);

            Assert.AreNotEqual(first, second);
            Assert.IsGreaterThan(0, second);
        }

        [TestMethod]
        public async Task ClaimNextAsync_ClaimsItemUnderLeaseAndIncrementsAttemptCount()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);

            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            Assert.IsNotNull(claimed);
            Assert.AreEqual(queueId, claimed.Id);
            Assert.AreEqual(DuplicateDetectionStatus.Running, claimed.Status);
            Assert.AreEqual(1, claimed.AttemptCount);
            Assert.IsNotNull(claimed.LeasedUntilUtc);
        }

        [TestMethod]
        public async Task ClaimNextAsync_ReturnsNullWhenNothingDue()
        {
            using DuplicateDetectionTestDatabase database = new();

            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            Assert.IsNull(claimed);
        }

        [TestMethod]
        public async Task ClaimNextAsync_ReclaimsExpiredLease()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DateTime now = DateTime.UtcNow;

            DuplicateDetectionQueueItem first = await database.Queue.ClaimNextAsync(now, TimeSpan.FromSeconds(-1), TestContext.CancellationToken);
            DuplicateDetectionQueueItem second = await database.Queue.ClaimNextAsync(now.AddSeconds(1), TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            Assert.AreEqual(queueId, first.Id);
            Assert.AreEqual(queueId, second.Id);
            Assert.AreEqual(2, second.AttemptCount);
        }

        [TestMethod]
        public async Task CompleteAsync_MarksItemCompletedAndRemovesItFromQueue()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            _ = await database.Queue.CompleteAsync(claimed.Id, claimed.AttemptCount, TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem next = await database.Queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            Assert.AreEqual((int)DuplicateDetectionStatus.Completed, status);
            Assert.AreEqual(string.Empty, lastError);
            Assert.IsNull(next);
        }

        [TestMethod]
        public async Task FailAsync_MarksItemFailedWithErrorAndRemovesItFromQueue()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            _ = await database.Queue.FailAsync(claimed.Id, claimed.AttemptCount, "boom", TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem next = await database.Queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            Assert.AreEqual((int)DuplicateDetectionStatus.Failed, status);
            Assert.AreEqual("boom", lastError);
            Assert.IsNull(next);
        }

        [TestMethod]
        public async Task ReleaseForRetryAsync_ReturnsToPendingAndHonoursNextAttempt()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DateTime now = DateTime.UtcNow;
            DuplicateDetectionQueueItem claimed = await database.Queue.ClaimNextAsync(now, TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            DateTime nextAttempt = now.AddMinutes(5);

            _ = await database.Queue.ReleaseForRetryAsync(claimed.Id, claimed.AttemptCount, nextAttempt, "retry later", TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            DuplicateDetectionQueueItem beforeDue = await database.Queue.ClaimNextAsync(now.AddMinutes(1), TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            DuplicateDetectionQueueItem afterDue = await database.Queue.ClaimNextAsync(nextAttempt, TimeSpan.FromMinutes(1), TestContext.CancellationToken);
            Assert.AreEqual((int)DuplicateDetectionStatus.Pending, status);
            Assert.AreEqual("retry later", lastError);
            Assert.IsNull(beforeDue);
            Assert.IsNotNull(afterDue);
            Assert.AreEqual(queueId, afterDue.Id);
        }

        [TestMethod]
        public async Task CompleteAsync_WithStaleAttempt_ReturnsFalseAndLeavesItemRunning()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DateTime now = DateTime.UtcNow;

            DuplicateDetectionQueueItem first = await database.Queue.ClaimNextAsync(now, TimeSpan.FromSeconds(-1), TestContext.CancellationToken);
            _ = await database.Queue.ClaimNextAsync(now.AddSeconds(1), TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            bool completed = await database.Queue.CompleteAsync(first.Id, first.AttemptCount, TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            Assert.IsFalse(completed);
            Assert.AreEqual((int)DuplicateDetectionStatus.Running, status);
        }

        [TestMethod]
        public async Task FailAsync_WithStaleAttempt_ReturnsFalseAndLeavesItemRunning()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DateTime now = DateTime.UtcNow;

            DuplicateDetectionQueueItem first = await database.Queue.ClaimNextAsync(now, TimeSpan.FromSeconds(-1), TestContext.CancellationToken);
            _ = await database.Queue.ClaimNextAsync(now.AddSeconds(1), TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            bool failed = await database.Queue.FailAsync(first.Id, first.AttemptCount, "boom", TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            Assert.IsFalse(failed);
            Assert.AreEqual((int)DuplicateDetectionStatus.Running, status);
            Assert.AreEqual(string.Empty, lastError);
        }

        [TestMethod]
        public async Task ReleaseForRetryAsync_WithStaleAttempt_ReturnsFalseAndLeavesItemRunning()
        {
            using DuplicateDetectionTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long queueId = await database.EnqueueAsync(findingId, TestContext.CancellationToken);
            DateTime now = DateTime.UtcNow;

            DuplicateDetectionQueueItem first = await database.Queue.ClaimNextAsync(now, TimeSpan.FromSeconds(-1), TestContext.CancellationToken);
            _ = await database.Queue.ClaimNextAsync(now.AddSeconds(1), TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            bool released = await database.Queue.ReleaseForRetryAsync(first.Id, first.AttemptCount, now.AddMinutes(5), "retry", TestContext.CancellationToken);

            (int status, string lastError) = await database.GetQueueRowAsync(queueId, TestContext.CancellationToken);
            Assert.IsFalse(released);
            Assert.AreEqual((int)DuplicateDetectionStatus.Running, status);
            Assert.AreEqual(string.Empty, lastError);
        }

        private sealed class DuplicateDetectionTestDatabase : IDisposable
        {
            private readonly string _connectionString;
            private readonly string _directory;

            public DuplicateDetectionTestDatabase()
            {
                _directory = Path.Combine(Path.GetTempPath(), "revathryn-data-tests", Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(_directory);

                _connectionString = $"Data Source={Path.Combine(_directory, "test.db")};Pooling=False";
                DatabaseOptions options = new(new RevathrynSettings("test")) { ConnectionString = _connectionString };
                SqliteConnectionFactory factory = new(options);
                SqliteDatabaseInitializer initializer = new(factory);
                initializer.Initialize();

                Queue = new DuplicateDetectionRepository(factory);
                Findings = new FindingRepository(factory);

                RepositoryRepository repositories = new(factory);
                DateTime now = DateTime.UtcNow;
                RepositoryId = repositories
                    .AddAsync(new Repository { Name = "Repo", LocalPath = "C:\\src\\Repo", Enabled = true, CreatedDate = now, LastModifiedDate = now })
                    .GetAwaiter()
                    .GetResult();
            }

            public long RepositoryId { get; }

            public DuplicateDetectionRepository Queue { get; }

            public FindingRepository Findings { get; }

            public async Task<long> AddFindingAsync(CancellationToken cancellationToken)
            {
                DateTime now = DateTime.UtcNow;

                return await Findings.AddAsync(new Finding
                {
                    RepositoryId = RepositoryId,
                    Fingerprint = Guid.NewGuid().ToString("N"),
                    Severity = FindingSeverity.High,
                    Title = "Title",
                    Description = "Description",
                    SuggestedFix = "Fix",
                    Status = FindingStatus.New,
                    Category = FindingCategory.CodeIssue,
                    CreatedDate = now,
                    LastModifiedDate = now
                }, cancellationToken);
            }

            public async Task<long> EnqueueAsync(long findingId, CancellationToken cancellationToken)
            {
                return await Queue.EnqueueAsync(RepositoryId, findingId, cancellationToken);
            }

            public async Task<(int Status, string LastError)> GetQueueRowAsync(long id, CancellationToken cancellationToken)
            {
                using SqliteConnection connection = new(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT Status, LastError FROM DuplicateDetectionQueue WHERE Id = @Id;";
                command.Parameters.AddWithValue("@Id", id);

                using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                _ = await reader.ReadAsync(cancellationToken);
                return (reader.GetInt32(0), reader.GetString(1));
            }

            public void Dispose()
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }
        }
    }
}
