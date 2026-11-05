using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class FindingRepositoryDuplicateTests
    {
        private const double RecordedSimilarity = 0.85;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task GetSimilarityCandidatesAsync_ReturnsOnlyCodeIssueFindingsOrderedById()
        {
            using FindingDuplicateTestDatabase database = new();
            long first = await database.AddFindingAsync(database.RepositoryId, "fp-1", "first", FindingCategory.CodeIssue, TestContext.CancellationToken);
            long second = await database.AddFindingAsync(database.RepositoryId, "fp-2", "second", FindingCategory.CodeIssue, TestContext.CancellationToken);
            _ = await database.AddFindingAsync(database.RepositoryId, "fp-3", "operational", FindingCategory.OperationalFailure, TestContext.CancellationToken);

            IReadOnlyList<Finding> candidates = await database.Findings.GetSimilarityCandidatesAsync(database.RepositoryId, TestContext.CancellationToken);

            Assert.HasCount(2, candidates);
            Assert.AreEqual(first, candidates[0].Id);
            Assert.AreEqual(second, candidates[1].Id);
        }

        [TestMethod]
        public async Task GetSimilarityCandidatesAsync_ExcludesFindingsFromOtherRepositories()
        {
            using FindingDuplicateTestDatabase database = new();
            long local = await database.AddFindingAsync(database.RepositoryId, "fp-1", "local", FindingCategory.CodeIssue, TestContext.CancellationToken);
            _ = await database.AddFindingAsync(database.OtherRepositoryId, "fp-2", "other", FindingCategory.CodeIssue, TestContext.CancellationToken);

            IReadOnlyList<Finding> candidates = await database.Findings.GetSimilarityCandidatesAsync(database.RepositoryId, TestContext.CancellationToken);

            Assert.HasCount(1, candidates);
            Assert.AreEqual(local, candidates[0].Id);
        }

        [TestMethod]
        public async Task MarkDuplicateAsync_RecordsDuplicateOfScoreAndModifiedDate()
        {
            using FindingDuplicateTestDatabase database = new();
            long originalId = await database.AddFindingAsync(database.RepositoryId, "fp-1", "original", FindingCategory.CodeIssue, TestContext.CancellationToken);
            long duplicateId = await database.AddFindingAsync(database.RepositoryId, "fp-2", "duplicate", FindingCategory.CodeIssue, TestContext.CancellationToken);
            DateTime modified = DateTime.UtcNow.AddHours(1);

            await database.Findings.MarkDuplicateAsync(duplicateId, originalId, RecordedSimilarity, modified, TestContext.CancellationToken);

            Finding stored = await database.Findings.GetByIdAsync(duplicateId, TestContext.CancellationToken);
            Assert.AreEqual(originalId, stored.DuplicateOfFindingId.Value);
            Assert.AreEqual(RecordedSimilarity, stored.DuplicateSimilarityScore.Value);
            Assert.AreEqual(modified, stored.LastModifiedDate);
        }

        [TestMethod]
        public async Task AddAsync_RoundTripsDuplicateColumns()
        {
            using FindingDuplicateTestDatabase database = new();
            long originalId = await database.AddFindingAsync(database.RepositoryId, "fp-1", "original", FindingCategory.CodeIssue, TestContext.CancellationToken);

            long duplicateId = await database.AddFindingAsync(
                database.RepositoryId,
                "fp-2",
                "duplicate",
                FindingCategory.CodeIssue,
                TestContext.CancellationToken,
                duplicateOfFindingId: originalId,
                similarityScore: RecordedSimilarity);

            Finding stored = await database.Findings.GetByIdAsync(duplicateId, TestContext.CancellationToken);
            Assert.AreEqual(originalId, stored.DuplicateOfFindingId.Value);
            Assert.AreEqual(RecordedSimilarity, stored.DuplicateSimilarityScore.Value);
        }

        private sealed class FindingDuplicateTestDatabase : IDisposable
        {
            private readonly string _directory;
            private readonly RepositoryRepository _repositories;

            public FindingDuplicateTestDatabase()
            {
                _directory = Path.Combine(Path.GetTempPath(), "revathryn-data-tests", Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(_directory);

                string connectionString = $"Data Source={Path.Combine(_directory, "test.db")};Pooling=False";
                DatabaseOptions options = new(new RevathrynSettings("test")) { ConnectionString = connectionString };
                SqliteConnectionFactory factory = new(options);
                SqliteDatabaseInitializer initializer = new(factory);
                initializer.Initialize();

                Findings = new FindingRepository(factory);
                _repositories = new RepositoryRepository(factory);

                RepositoryId = CreateRepository("Repo");
                OtherRepositoryId = CreateRepository("Other");
            }

            public long RepositoryId { get; }

            public long OtherRepositoryId { get; }

            public FindingRepository Findings { get; }

            public async Task<long> AddFindingAsync(
                long repositoryId,
                string fingerprint,
                string title,
                FindingCategory category,
                CancellationToken cancellationToken,
                long? duplicateOfFindingId = null,
                double? similarityScore = null)
            {
                DateTime now = DateTime.UtcNow;

                return await Findings.AddAsync(new Finding
                {
                    RepositoryId = repositoryId,
                    Fingerprint = fingerprint,
                    Severity = FindingSeverity.High,
                    Title = title,
                    Description = "Description",
                    SuggestedFix = "Suggested fix",
                    Status = FindingStatus.New,
                    Category = category,
                    CreatedDate = now,
                    LastModifiedDate = now,
                    DuplicateOfFindingId = duplicateOfFindingId,
                    DuplicateSimilarityScore = similarityScore
                }, cancellationToken);
            }

            public void Dispose()
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }

            private long CreateRepository(string name)
            {
                DateTime now = DateTime.UtcNow;

                return _repositories
                    .AddAsync(new Repository { Name = name, LocalPath = $"C:\\src\\{name}", Enabled = true, CreatedDate = now, LastModifiedDate = now })
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }
}
