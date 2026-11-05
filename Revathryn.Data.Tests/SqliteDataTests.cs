using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SqliteDataTests
    {
        [TestMethod]
        public async Task ReviewUsageRepository_PersistsAndAggregatesSummary()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewUsageRepository usageRepository = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            long todayId = await usageRepository.AddAsync(new ReviewUsage
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                EngineId = 1,
                EngineName = "GPT",
                EngineModel = "gpt-4o",
                AttemptCount = 2,
                PromptTokens = 100,
                CompletionTokens = 50,
                TotalTokens = 150,
                CachedTokens = 80,
                OccurredUtc = DateTime.UtcNow
            }, TestContext.CancellationToken);

            long yesterdayId = await usageRepository.AddAsync(new ReviewUsage
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                EngineId = 2,
                EngineName = "Claude",
                EngineModel = "claude-3",
                AttemptCount = 1,
                PromptTokens = 40,
                CompletionTokens = 20,
                TotalTokens = 60,
                CachedTokens = 10,
                OccurredUtc = DateTime.UtcNow.AddDays(-1)
            }, TestContext.CancellationToken);

            IReadOnlyList<ReviewUsage> all = await usageRepository.GetAllAsync(TestContext.CancellationToken);
            Assert.HasCount(2, all);
            Assert.AreEqual(todayId, all[0].Id);
            Assert.AreEqual(yesterdayId, all[1].Id);

            ReviewUsageSummary summary = await usageRepository.GetSummaryAsync(TestContext.CancellationToken);
            Assert.AreEqual(2, summary.TotalReviewCount);
            Assert.AreEqual(140, summary.TotalPromptTokens);
            Assert.AreEqual(70, summary.TotalCompletionTokens);
            Assert.AreEqual(210, summary.TotalTokens);
            Assert.AreEqual(90, summary.TotalCachedTokens);
            Assert.AreEqual(1, summary.TodayReviewCount);
            Assert.AreEqual(100, summary.TodayPromptTokens);
            Assert.AreEqual(150, summary.TodayTotalTokens);
            Assert.AreEqual(80, summary.TodayCachedTokens);
        }

        [TestMethod]
        public async Task RepositoryRepository_PersistsAndReturnsRepository()
        {
            using TestDatabase database = new();
            RepositoryRepository repository = new(database.Factory);

            DateTime now = DateTime.UtcNow;
            long id = await repository.AddAsync(new Repository
            {
                Name = "Example",
                Description = "A sample repository",
                LocalPath = "C:\\src\\example",
                Enabled = true,
                AutomaticReviewEnabled = true,
                MaxContextRequests = 3,
                CreatedDate = now,
                LastModifiedDate = now
            }, TestContext.CancellationToken);

            Repository loaded = await repository.GetByIdAsync(id, TestContext.CancellationToken);

            Assert.AreEqual(id, loaded.Id);
            Assert.AreEqual("Example", loaded.Name);
            Assert.AreEqual("C:\\src\\example", loaded.LocalPath);
            Assert.IsTrue(loaded.Enabled);
            Assert.IsTrue(loaded.AutomaticReviewEnabled);
            Assert.AreEqual(3, loaded.MaxContextRequests);
        }

        [TestMethod]
        public async Task RepoFileRepository_Upsert_DeduplicatesByRepositoryAndRelativePath()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            long firstId = await files.UpsertAsync(new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = "src/Program.cs",
                FileName = "Program.cs",
                Extension = "cs",
                Sha256 = "aaa",
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            }, TestContext.CancellationToken);

            long secondId = await files.UpsertAsync(new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = "src/Program.cs",
                FileName = "Program.cs",
                Extension = "cs",
                Sha256 = "bbb",
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            }, TestContext.CancellationToken);

            Assert.AreEqual(firstId, secondId);

            IReadOnlyList<RepoFile> all = await files.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);
            Assert.HasCount(1, all);
            Assert.AreEqual("bbb", all[0].Sha256);
        }

        [TestMethod]
        public async Task QueueRepository_PreventsDuplicatePendingItems()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            QueueItem item = CreateQueueItem(repositoryId, fileId, reviewTypeId, 10);

            bool first = await queue.EnqueueAsync(item, TestContext.CancellationToken);
            bool second = await queue.EnqueueAsync(item, TestContext.CancellationToken);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, await queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task QueueRepository_ClaimsHighestPriorityFirst()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileA = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long fileB = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/B.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileA, reviewTypeId, 1), TestContext.CancellationToken);
            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileB, reviewTypeId, 50), TestContext.CancellationToken);

            QueueItem claimed = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);

            Assert.AreEqual(fileB, claimed.RepoFileId);
            Assert.AreEqual(50, claimed.Priority);
            Assert.AreEqual(QueueItemStatus.InFlight, claimed.Status);
            Assert.AreEqual(1, claimed.AttemptCount);
            Assert.AreEqual(1, await queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task QueueRepository_ReleaseForRetry_ReturnsItemToPendingWithBackoff()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10), TestContext.CancellationToken);

            QueueItem claimed = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);
            DateTime nextAttemptUtc = DateTime.UtcNow.AddMinutes(2);

            await queue.ReleaseForRetryAsync(claimed.Id, 5, nextAttemptUtc, "failure", TestContext.CancellationToken);

            Assert.AreEqual(1, await queue.CountAsync(TestContext.CancellationToken));
            IReadOnlyList<QueueItem> pending = await queue.GetPendingAsync(TestContext.CancellationToken);
            Assert.HasCount(1, pending);
            Assert.AreEqual(QueueItemStatus.Pending, pending[0].Status);
            Assert.AreEqual(5, pending[0].Priority);
            Assert.AreEqual("failure", pending[0].LastError);
            Assert.IsTrue(pending[0].NextAttemptUtc.HasValue);
            Assert.IsGreaterThanOrEqualTo(nextAttemptUtc.AddSeconds(-5), pending[0].NextAttemptUtc.Value);
        }

        [TestMethod]
        public async Task QueueRepository_ClaimNext_SkipsItemsBeforeNextAttempt()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10), TestContext.CancellationToken);

            QueueItem claimed = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);
            await queue.ReleaseForRetryAsync(claimed.Id, 5, DateTime.UtcNow.AddMinutes(5), "failure", TestContext.CancellationToken);

            QueueItem notEligible = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(10), TestContext.CancellationToken);
            Assert.IsNull(notEligible);

            await queue.ReleaseForRetryAsync(claimed.Id, 5, DateTime.UtcNow.AddSeconds(-1), "failure", TestContext.CancellationToken);
            QueueItem eligible = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(10), TestContext.CancellationToken);
            Assert.IsNotNull(eligible);
            Assert.AreEqual(claimed.Id, eligible.Id);
            Assert.AreEqual(2, eligible.AttemptCount);
        }

        [TestMethod]
        public async Task QueueRepository_ClaimNext_ReclaimsStaleLease()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10), TestContext.CancellationToken);

            QueueItem firstClaim = await queue.ClaimNextAsync(DateTime.UtcNow.AddSeconds(-1), TestContext.CancellationToken);
            QueueItem reclaimed = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);

            Assert.IsNotNull(reclaimed);
            Assert.AreEqual(firstClaim.Id, reclaimed.Id);
            Assert.AreEqual(2, reclaimed.AttemptCount);
        }

        [TestMethod]
        public async Task QueueRepository_RemoveAsync_RemovesItemFromQueue()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10), TestContext.CancellationToken);

            QueueItem claimed = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);
            await queue.RemoveAsync(claimed.Id, TestContext.CancellationToken);

            Assert.AreEqual(0, await queue.CountAsync(TestContext.CancellationToken));
            QueueMetrics metrics = await queue.GetMetricsAsync(TestContext.CancellationToken);
            Assert.AreEqual(0, metrics.PendingCount);
            Assert.AreEqual(0, metrics.RetryCount);
        }

        [TestMethod]
        public async Task QueueRepository_GetMetrics_CountsPendingAndRetry()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileA = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long fileB = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/B.cs"), TestContext.CancellationToken);
            long fileC = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/C.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileA, reviewTypeId, 10), TestContext.CancellationToken);
            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileB, reviewTypeId, 20), TestContext.CancellationToken);
            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileC, reviewTypeId, 30), TestContext.CancellationToken);

            QueueItem retried = await queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);
            await queue.ReleaseForRetryAsync(retried.Id, retried.Priority, DateTime.UtcNow.AddSeconds(-1), "transient", TestContext.CancellationToken);

            QueueMetrics metrics = await queue.GetMetricsAsync(TestContext.CancellationToken);

            Assert.AreEqual(3, metrics.PendingCount);
            Assert.AreEqual(1, metrics.RetryCount);
            Assert.IsTrue(metrics.OldestPendingUtc.HasValue);
        }

        [TestMethod]
        public async Task FindingRepository_TracksFingerprintsForDuplicateDetection()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            Finding finding = new()
            {
                RepositoryId = repositoryId,
                Fingerprint = "fp-001",
                Severity = FindingSeverity.High,
                Title = "Issue",
                Description = "Description",
                Status = FindingStatus.New,
                Category = FindingCategory.CodeIssue,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            long id = await findings.AddAsync(finding, TestContext.CancellationToken);

            Finding byFingerprint = await findings.GetByFingerprintAsync(repositoryId, "fp-001", TestContext.CancellationToken);

            Assert.AreEqual(id, byFingerprint.Id);
            Assert.AreEqual(FindingSeverity.High, byFingerprint.Severity);
        }

        [TestMethod]
        public async Task RepoFileAnalysisRepository_TracksFingerprintPerReviewType()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            RepoFileAnalysisRepository analysis = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            RepoFileAnalysis record = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-1",
                LastAnalysedDate = DateTime.UtcNow,
                AnalysisVersion = "1.0"
            };

            long firstId = await analysis.UpsertAsync(record, TestContext.CancellationToken);
            record.Sha256 = "hash-2";
            long secondId = await analysis.UpsertAsync(record, TestContext.CancellationToken);

            Assert.AreEqual(firstId, secondId);

            RepoFileAnalysis loaded = await analysis.GetAsync(fileId, reviewTypeId, TestContext.CancellationToken);
            Assert.AreEqual("hash-2", loaded.Sha256);
        }

        [TestMethod]
        public async Task FindingRepository_DuplicateFingerprint_ReturnsExistingId()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            Finding finding = CreateFinding(repositoryId, "dup-fp");

            long firstId = await findings.AddAsync(finding, TestContext.CancellationToken);
            long secondId = await findings.AddAsync(CreateFinding(repositoryId, "dup-fp"), TestContext.CancellationToken);

            Assert.AreEqual(firstId, secondId);
            Assert.HasCount(1, await findings.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task Finding_RepoFileDeletion_SetsRepoFileIdToNull()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);

            Finding finding = CreateFinding(repositoryId, "fp-file");
            finding.RepoFileId = fileId;
            long findingId = await findings.AddAsync(finding, TestContext.CancellationToken);

            await files.DeleteAsync(fileId, TestContext.CancellationToken);

            Finding retained = await findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            Assert.IsNotNull(retained);
            Assert.IsNull(retained.RepoFileId);
        }

        [TestMethod]
        public async Task RepoFileDeletion_CascadesDependentRecordsAndPreservesFindings()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewLinkRepository links = new(database.Factory);
            RepoFileAnalysisRepository analyses = new(database.Factory);
            QueueRepository queue = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await links.UpsertAsync(new ReviewLink
            {
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Enabled = true
            }, TestContext.CancellationToken);

            await analyses.UpsertAsync(new RepoFileAnalysis
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash",
                LastAnalysedDate = DateTime.UtcNow,
                AnalysisVersion = "1",
                ReviewDefinitionVersion = "v1"
            }, TestContext.CancellationToken);

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10), TestContext.CancellationToken);

            Finding finding = CreateFinding(repositoryId, "fp-cascade");
            finding.RepoFileId = fileId;
            long findingId = await findings.AddAsync(finding, TestContext.CancellationToken);

            await files.DeleteAsync(fileId, TestContext.CancellationToken);

            Assert.IsNull(await links.GetAsync(fileId, reviewTypeId, TestContext.CancellationToken));
            Assert.IsNull(await analyses.GetAsync(fileId, reviewTypeId, TestContext.CancellationToken));
            Assert.AreEqual(0, await queue.CountAsync(TestContext.CancellationToken));

            Finding retained = await findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            Assert.IsNotNull(retained);
            Assert.IsNull(retained.RepoFileId);
        }

        [TestMethod]
        public async Task QueueRepository_ConcurrentInsertionIsSafe()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            QueueItem item = CreateQueueItem(repositoryId, fileId, reviewTypeId, 10);

            Task<bool>[] attempts =
            [
                queue.EnqueueAsync(item, TestContext.CancellationToken),
                queue.EnqueueAsync(item, TestContext.CancellationToken),
                queue.EnqueueAsync(item, TestContext.CancellationToken),
                queue.EnqueueAsync(item, TestContext.CancellationToken),
                queue.EnqueueAsync(item, TestContext.CancellationToken),
                queue.EnqueueAsync(item, TestContext.CancellationToken)
            ];

            bool[] results = await Task.WhenAll(attempts);

            Assert.HasCount(1, results.Where(r => r));
            Assert.AreEqual(1, await queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_TracksNewChangedUnchangedAndRemoved()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";
            RepoFile fileB = CreateRepoFile(repositoryId, "src/B.cs");
            fileB.Sha256 = "hash-b";

            RepositoryReconciliationResult first = await files.ReconcileAsync(repositoryId, [fileA, fileB], null, TestContext.CancellationToken);

            Assert.AreEqual(2, first.NewFiles);
            Assert.AreEqual(0, first.ChangedFiles);
            Assert.AreEqual(0, first.UnchangedFiles);
            Assert.AreEqual(0, first.RemovedFiles);

            RepoFile fileAUnchanged = CreateRepoFile(repositoryId, "src/A.cs");
            fileAUnchanged.Sha256 = "hash-a";
            RepoFile fileBChanged = CreateRepoFile(repositoryId, "src/B.cs");
            fileBChanged.Sha256 = "hash-b2";
            RepoFile fileC = CreateRepoFile(repositoryId, "src/C.cs");
            fileC.Sha256 = "hash-c";

            RepositoryReconciliationResult second = await files.ReconcileAsync(repositoryId, [fileAUnchanged, fileBChanged, fileC], null, TestContext.CancellationToken);

            Assert.AreEqual(1, second.NewFiles);
            Assert.AreEqual(1, second.ChangedFiles);
            Assert.AreEqual(1, second.UnchangedFiles);
            Assert.AreEqual(0, second.RemovedFiles);

            RepositoryReconciliationResult third = await files.ReconcileAsync(repositoryId, [], null, TestContext.CancellationToken);

            Assert.AreEqual(3, third.RemovedFiles);
            Assert.AreEqual(0, third.NewFiles);
            Assert.AreEqual(0, third.ChangedFiles);
            Assert.AreEqual(0, third.UnchangedFiles);

            RepoFile removed = await files.GetByRelativePathAsync(repositoryId, "src/A.cs", TestContext.CancellationToken);
            Assert.IsTrue(removed.IsRemoved);
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_ReportsStructuredChanges()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";
            RepoFile fileB = CreateRepoFile(repositoryId, "src/B.cs");
            fileB.Sha256 = "hash-b";

            RepositoryReconciliationResult first = await files.ReconcileAsync(repositoryId, [fileA, fileB], null, TestContext.CancellationToken);

            Assert.HasCount(2, first.Changes.Where(change => change.ChangeType == RepositoryChangeType.Added));
            Assert.IsTrue(first.Changes.All(change => change.RepositoryId == repositoryId));

            RepoFile fileAUnchanged = CreateRepoFile(repositoryId, "src/A.cs");
            fileAUnchanged.Sha256 = "hash-a";
            RepoFile fileBChanged = CreateRepoFile(repositoryId, "src/B.cs");
            fileBChanged.Sha256 = "hash-b2";
            RepoFile fileC = CreateRepoFile(repositoryId, "src/C.cs");
            fileC.Sha256 = "hash-c";

            RepositoryReconciliationResult second = await files.ReconcileAsync(repositoryId, [fileAUnchanged, fileBChanged, fileC], null, TestContext.CancellationToken);

            RepositoryFileChange unchanged = second.Changes.Single(change => change.ChangeType == RepositoryChangeType.Unchanged);
            RepositoryFileChange modified = second.Changes.Single(change => change.ChangeType == RepositoryChangeType.Modified);
            RepositoryFileChange added = second.Changes.Single(change => change.ChangeType == RepositoryChangeType.Added);

            Assert.AreEqual("src/A.cs", unchanged.RelativePath);
            Assert.AreEqual("hash-a", unchanged.Sha256);
            Assert.AreEqual("src/B.cs", modified.RelativePath);
            Assert.AreEqual("hash-b2", modified.Sha256);
            Assert.AreEqual("src/C.cs", added.RelativePath);
            Assert.AreEqual("hash-c", added.Sha256);

            RepositoryReconciliationResult third = await files.ReconcileAsync(repositoryId, [], null, TestContext.CancellationToken);

            Assert.HasCount(3, third.Changes.Where(change => change.ChangeType == RepositoryChangeType.Removed));
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_DetectsRenamedFile()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";
            fileA.FileSize = 100;

            await files.ReconcileAsync(repositoryId, [fileA], null, TestContext.CancellationToken);

            RepoFile fileB = CreateRepoFile(repositoryId, "src/B.cs");
            fileB.Sha256 = "hash-a";
            fileB.FileSize = 100;

            RepositoryReconciliationResult result = await files.ReconcileAsync(repositoryId, [fileB], null, TestContext.CancellationToken);

            Assert.AreEqual(1, result.RenamedFiles);
            Assert.AreEqual(0, result.NewFiles);
            Assert.AreEqual(0, result.RemovedFiles);
            Assert.AreEqual(0, result.ChangedFiles);
            Assert.AreEqual(0, result.UnchangedFiles);

            RepositoryFileChange change = result.Changes.Single(rename => rename.ChangeType == RepositoryChangeType.Renamed);
            Assert.AreEqual("src/B.cs", change.RelativePath);
            Assert.AreEqual("src/A.cs", change.PreviousRelativePath);
            Assert.AreEqual("hash-a", change.Sha256);

            RepoFile oldPath = await files.GetByRelativePathAsync(repositoryId, "src/A.cs", TestContext.CancellationToken);
            RepoFile newPath = await files.GetByRelativePathAsync(repositoryId, "src/B.cs", TestContext.CancellationToken);

            Assert.IsTrue(oldPath.IsRemoved);
            Assert.IsFalse(newPath.IsRemoved);
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_PreservesFailedPaths()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";

            await files.ReconcileAsync(repositoryId, [fileA], null, TestContext.CancellationToken);

            RepositoryReconciliationResult result = await files.ReconcileAsync(repositoryId, [], ["src/A.cs"], TestContext.CancellationToken);

            Assert.AreEqual(0, result.RemovedFiles);

            RepoFile retained = await files.GetByRelativePathAsync(repositoryId, "src/A.cs", TestContext.CancellationToken);
            Assert.IsFalse(retained.IsRemoved);
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_ReintroducedFile_RestoresInventory()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";

            await files.ReconcileAsync(repositoryId, [fileA], null, TestContext.CancellationToken);
            await files.ReconcileAsync(repositoryId, [], null, TestContext.CancellationToken);

            RepoFile removed = await files.GetByRelativePathAsync(repositoryId, "src/A.cs", TestContext.CancellationToken);
            Assert.IsTrue(removed.IsRemoved);

            RepoFile reintroduced = CreateRepoFile(repositoryId, "src/A.cs");
            reintroduced.Sha256 = "hash-a";

            RepositoryReconciliationResult result = await files.ReconcileAsync(repositoryId, [reintroduced], null, TestContext.CancellationToken);

            Assert.AreEqual(1, result.UnchangedFiles);
            Assert.AreEqual(0, result.RemovedFiles);

            RepoFile restored = await files.GetByRelativePathAsync(repositoryId, "src/A.cs", TestContext.CancellationToken);
            Assert.IsFalse(restored.IsRemoved);
        }

        [TestMethod]
        public async Task ProjectRepository_Reconcile_LinksProjectsToSolution()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            ProjectRepository projects = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepositoryProject solution = new() { Name = "Revathryn", Kind = ProjectKind.Solution, RelativePath = "Revathryn.sln" };
            RepositoryProject app = new() { Name = "App", Kind = ProjectKind.Project, RelativePath = "src/App/App.csproj", DirectoryPath = "src/App", SolutionPath = "Revathryn.sln" };
            RepositoryProject nested = new() { Name = "Nested", Kind = ProjectKind.Project, RelativePath = "src/Nested/Nested.csproj", DirectoryPath = "src/Nested", SolutionPath = "Revathryn.sln" };

            await projects.ReconcileAsync(repositoryId, [solution, app, nested], TestContext.CancellationToken);

            IReadOnlyList<RepositoryProject> loaded = await projects.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);

            Assert.HasCount(3, loaded);

            RepositoryProject loadedSolution = loaded.Single(p => p.Kind == ProjectKind.Solution);
            RepositoryProject loadedApp = loaded.Single(p => p.Name == "App");
            RepositoryProject loadedNested = loaded.Single(p => p.Name == "Nested");

            Assert.AreEqual(loadedSolution.Id, loadedApp.SolutionId);
            Assert.AreEqual(loadedSolution.Id, loadedNested.SolutionId);
        }

        [TestMethod]
        public async Task ProjectRepository_Reconcile_RemovesMissingProjects()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            ProjectRepository projects = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            RepositoryProject solution = new() { Name = "Revathryn", Kind = ProjectKind.Solution, RelativePath = "Revathryn.sln" };
            RepositoryProject app = new() { Name = "App", Kind = ProjectKind.Project, RelativePath = "src/App/App.csproj", DirectoryPath = "src/App", SolutionPath = "Revathryn.sln" };

            await projects.ReconcileAsync(repositoryId, [solution, app], TestContext.CancellationToken);
            await projects.ReconcileAsync(repositoryId, [solution], TestContext.CancellationToken);

            IReadOnlyList<RepositoryProject> remaining = await projects.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);

            Assert.HasCount(1, remaining);
            Assert.AreEqual(ProjectKind.Solution, remaining[0].Kind);
        }

        [TestMethod]
        public async Task RepositoryScanRepository_PersistsRecords_NewestFirst()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepositoryScanRepository scans = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            DateTime older = DateTime.UtcNow.AddMinutes(-5);
            DateTime newer = DateTime.UtcNow;

            long olderId = await scans.AddAsync(new RepositoryScanRecord
            {
                RepositoryId = repositoryId,
                StartedUtc = older,
                CompletedUtc = older.AddSeconds(1),
                Succeeded = true,
                FilesDiscovered = 1,
                NewFiles = 1
            }, TestContext.CancellationToken);

            long newerId = await scans.AddAsync(new RepositoryScanRecord
            {
                RepositoryId = repositoryId,
                StartedUtc = newer,
                CompletedUtc = newer.AddSeconds(1),
                Succeeded = false
            }, TestContext.CancellationToken);

            IReadOnlyList<RepositoryScanRecord> records = await scans.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);

            Assert.HasCount(2, records);
            Assert.AreEqual(newerId, records[0].Id);
            Assert.AreEqual(olderId, records[1].Id);
            Assert.IsFalse(records[0].Succeeded);
            Assert.IsTrue(records[1].Succeeded);
        }

        [TestMethod]
        public async Task RepositoryRepository_RoundTripsScanOptionsAndLastSuccessfulScanDate()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);

            DateTime now = DateTime.UtcNow;
            RepositoryScanOptions options = new()
            {
                RespectGitIgnore = true,
                IncludedExtensions = ["cs", "json"],
                ExcludedDirectoryNames = ["docs"]
            };

            long id = await repositories.AddAsync(new Repository
            {
                Name = "Repo",
                LocalPath = "C:\\src\\Repo",
                Enabled = true,
                ScanOptions = options,
                LastSuccessfulScanDate = now,
                CreatedDate = now,
                LastModifiedDate = now
            }, TestContext.CancellationToken);

            Repository loaded = await repositories.GetByIdAsync(id, TestContext.CancellationToken);

            Assert.IsNotNull(loaded.ScanOptions);
            Assert.IsTrue(loaded.ScanOptions.RespectGitIgnore);
            CollectionAssert.AreEquivalent(expected, loaded.ScanOptions.IncludedExtensions.ToArray());
            CollectionAssert.AreEquivalent(expectedArray, loaded.ScanOptions.ExcludedDirectoryNames.ToArray());
            Assert.IsNotNull(loaded.LastSuccessfulScanDate);
            Assert.AreEqual(now, loaded.LastSuccessfulScanDate.Value);
        }

        [TestMethod]
        public async Task RepositoryRepository_NullScanState_RoundTripsAsNull()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);

            long id = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            Repository loaded = await repositories.GetByIdAsync(id, TestContext.CancellationToken);

            Assert.IsNull(loaded.ScanOptions);
            Assert.IsNull(loaded.LastSuccessfulScanDate);
        }

        [TestMethod]
        public async Task ReviewContextRepository_PersistsAndReturnsOriginalContext()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewContextRepository contexts = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/Service.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            AIReviewContext context = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "abc123",
                Language = "CSharp",
                FilePath = "src/Service.cs",
                ReviewInstructions = "You are a code reviewer. Find issues.",
                FileContent = "public sealed class Service { }",
                ContextItems =
                [
                    new ReviewContextItem { Title = "Interface", SourcePath = "src/IService.cs", Content = "public interface IService { }" },
                    new ReviewContextItem { Title = "Project", SourcePath = "src/app.csproj", Content = "<Project />" }
                ],
                CreatedUtc = DateTime.UtcNow
            };

            long contextId = await contexts.AddAsync(context, TestContext.CancellationToken);

            AIReviewContext loaded = await contexts.GetByIdAsync(contextId, TestContext.CancellationToken);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(contextId, loaded.Id);
            Assert.AreEqual("You are a code reviewer. Find issues.", loaded.ReviewInstructions);
            Assert.AreEqual("public sealed class Service { }", loaded.FileContent);
            Assert.HasCount(2, loaded.ContextItems);
            Assert.AreEqual("Interface", loaded.ContextItems[0].Title);
            Assert.AreEqual("src/IService.cs", loaded.ContextItems[0].SourcePath);
            Assert.AreEqual("public interface IService { }", loaded.ContextItems[0].Content);
        }

        [TestMethod]
        public async Task ReviewContextRepository_ReturnsNullWhenMissing()
        {
            using TestDatabase database = new();
            ReviewContextRepository contexts = new(database.Factory);

            AIReviewContext loaded = await contexts.GetByIdAsync(999, TestContext.CancellationToken);

            Assert.IsNull(loaded);
        }

        [TestMethod]
        public async Task ReviewContextRepository_EmptyContextItemsRoundTrips()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewContextRepository contexts = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/Empty.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            long contextId = await contexts.AddAsync(new AIReviewContext
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "empty",
                ReviewInstructions = "instructions",
                FileContent = "content",
                ContextItems = [],
                CreatedUtc = DateTime.UtcNow
            }, TestContext.CancellationToken);

            AIReviewContext loaded = await contexts.GetByIdAsync(contextId, TestContext.CancellationToken);

            Assert.IsNotNull(loaded);
            Assert.HasCount(0, loaded.ContextItems);
        }

        [TestMethod]
        public async Task ReviewContextRepository_LargePayloadPreservedAccurately()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewContextRepository contexts = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/Large.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            string largeContent = new('x', 200000);
            string largeInstructions = string.Join(Environment.NewLine, Enumerable.Repeat("Always review the supplied source.", 2000));

            long contextId = await contexts.AddAsync(new AIReviewContext
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "large",
                ReviewInstructions = largeInstructions,
                FileContent = largeContent,
                ContextItems =
                [
                    new ReviewContextItem { Title = "Large", SourcePath = "src/Large.cs", Content = largeContent }
                ],
                CreatedUtc = DateTime.UtcNow
            }, TestContext.CancellationToken);

            AIReviewContext loaded = await contexts.GetByIdAsync(contextId, TestContext.CancellationToken);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(largeInstructions, loaded.ReviewInstructions);
            Assert.AreEqual(largeContent, loaded.FileContent);
            Assert.HasCount(1, loaded.ContextItems);
            Assert.AreEqual(largeContent, loaded.ContextItems[0].Content);
        }

        [TestMethod]
        public async Task ReviewOutcomeStore_LinksFindingsToPersistedContext()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewContextRepository contexts = new(database.Factory);
            ReviewOutcomeStore store = new(database.Factory, new FindingReconciler());
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            ReviewOutcome outcome = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-1",
                Findings =
                [
                    CreateFinding(repositoryId, "fp-1"),
                    CreateFinding(repositoryId, "fp-2")
                ],
                Context = new AIReviewContext
                {
                    RepositoryId = repositoryId,
                    RepoFileId = fileId,
                    ReviewTypeId = reviewTypeId,
                    Sha256 = "hash-1",
                    ReviewInstructions = "original instructions",
                    FileContent = "original content",
                    ContextItems =
                    [
                        new ReviewContextItem { Title = "Dependency", SourcePath = "src/Helper.cs", Content = "class Helper" }
                    ],
                    CreatedUtc = DateTime.UtcNow
                },
                CompletedUtc = DateTime.UtcNow
            };

            await store.PersistAsync(outcome, TestContext.CancellationToken);

            IReadOnlyList<Finding> stored = await findings.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);
            Assert.HasCount(2, stored);

            foreach (Finding finding in stored)
            {
                Assert.IsTrue(finding.ReviewContextId.HasValue);

                AIReviewContext context = await contexts.GetByIdAsync(finding.ReviewContextId.Value, TestContext.CancellationToken);
                Assert.IsNotNull(context);
                Assert.AreEqual("original instructions", context.ReviewInstructions);
                Assert.HasCount(1, context.ContextItems);
            }
        }

        [TestMethod]
        public async Task ReviewOutcomeStore_WithoutContextLeavesReviewContextIdNull()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewOutcomeStore store = new(database.Factory, new FindingReconciler());
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            ReviewOutcome outcome = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-2",
                Findings = [CreateFinding(repositoryId, "fp-no-context")],
                Context = null,
                CompletedUtc = DateTime.UtcNow
            };

            await store.PersistAsync(outcome, TestContext.CancellationToken);

            IReadOnlyList<Finding> stored = await findings.GetByRepositoryAsync(repositoryId, TestContext.CancellationToken);
            Assert.HasCount(1, stored);
            Assert.IsNull(stored[0].ReviewContextId);
        }

        [TestMethod]
        public async Task ReviewOutcomeStore_DoesNotAssociateContextAcrossExecutions()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewContextRepository contexts = new(database.Factory);
            ReviewOutcomeStore store = new(database.Factory, new FindingReconciler());
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            ReviewOutcome first = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-a",
                Findings = [CreateFinding(repositoryId, "fp-a")],
                Context = new AIReviewContext
                {
                    RepositoryId = repositoryId,
                    RepoFileId = fileId,
                    ReviewTypeId = reviewTypeId,
                    Sha256 = "hash-a",
                    ReviewInstructions = "first instructions",
                    FileContent = "first content",
                    CreatedUtc = DateTime.UtcNow
                },
                CompletedUtc = DateTime.UtcNow
            };

            ReviewOutcome second = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-b",
                Findings = [CreateFinding(repositoryId, "fp-b")],
                Context = new AIReviewContext
                {
                    RepositoryId = repositoryId,
                    RepoFileId = fileId,
                    ReviewTypeId = reviewTypeId,
                    Sha256 = "hash-b",
                    ReviewInstructions = "second instructions",
                    FileContent = "second content",
                    CreatedUtc = DateTime.UtcNow
                },
                CompletedUtc = DateTime.UtcNow
            };

            await store.PersistAsync(first, TestContext.CancellationToken);
            await store.PersistAsync(second, TestContext.CancellationToken);

            Finding firstFinding = await findings.GetByFingerprintAsync(repositoryId, "fp-a", TestContext.CancellationToken);
            Finding secondFinding = await findings.GetByFingerprintAsync(repositoryId, "fp-b", TestContext.CancellationToken);

            Assert.IsNotNull(firstFinding);
            Assert.IsNotNull(secondFinding);
            Assert.IsTrue(firstFinding.ReviewContextId.HasValue);
            Assert.IsTrue(secondFinding.ReviewContextId.HasValue);
            Assert.AreNotEqual(firstFinding.ReviewContextId.Value, secondFinding.ReviewContextId.Value);

            AIReviewContext firstContext = await contexts.GetByIdAsync(firstFinding.ReviewContextId.Value, TestContext.CancellationToken);
            AIReviewContext secondContext = await contexts.GetByIdAsync(secondFinding.ReviewContextId.Value, TestContext.CancellationToken);

            Assert.AreEqual("first instructions", firstContext.ReviewInstructions);
            Assert.AreEqual("second instructions", secondContext.ReviewInstructions);
        }

        [TestMethod]
        public async Task FindingRepository_QueryAsync_OrdersByPriorityDescendingByDefault()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            DateTime baseDate = DateTime.UtcNow;

            await findings.AddAsync(CreateFinding(repositoryId, "fp-info", FindingSeverity.Info, "Info issue", "info", baseDate.AddMinutes(5)), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-critical", FindingSeverity.Critical, "Critical issue", "critical", baseDate.AddMinutes(1)), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-high", FindingSeverity.High, "High issue", "high", baseDate.AddMinutes(4)), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-low", FindingSeverity.Low, "Low issue", "low", baseDate.AddMinutes(2)), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-medium", FindingSeverity.Medium, "Medium issue", "medium", baseDate.AddMinutes(3)), TestContext.CancellationToken);

            FindingQueryResult result = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = repositoryId,
                SortField = FindingSortField.Severity,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 100
            }, TestContext.CancellationToken);

            Assert.HasCount(5, result.Items);
            Assert.AreEqual(FindingSeverity.Critical, result.Items[0].Severity);
            Assert.AreEqual(FindingSeverity.High, result.Items[1].Severity);
            Assert.AreEqual(FindingSeverity.Medium, result.Items[2].Severity);
            Assert.AreEqual(FindingSeverity.Low, result.Items[3].Severity);
            Assert.AreEqual(FindingSeverity.Info, result.Items[4].Severity);
        }

        [TestMethod]
        public async Task FindingRepository_QueryAsync_IsolatesRepositories()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long firstRepositoryId = await repositories.AddAsync(CreateRepository("First"), TestContext.CancellationToken);
            long secondRepositoryId = await repositories.AddAsync(CreateRepository("Second"), TestContext.CancellationToken);

            await findings.AddAsync(CreateFinding(firstRepositoryId, "fp-first"), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(secondRepositoryId, "fp-second"), TestContext.CancellationToken);

            FindingQueryResult result = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = firstRepositoryId,
                SortField = FindingSortField.CreatedDate,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 100
            }, TestContext.CancellationToken);

            Assert.AreEqual(1, result.TotalCount);
            Assert.HasCount(1, result.Items);
            Assert.AreEqual("fp-first", result.Items[0].Fingerprint);
        }

        [TestMethod]
        public async Task FindingRepository_QueryAsync_FiltersBySeverityAndReviewType()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"), TestContext.CancellationToken);
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"), TestContext.CancellationToken);

            await findings.AddAsync(CreateFinding(repositoryId, "fp-1", FindingSeverity.High, "Title", "desc", DateTime.UtcNow, fileId, reviewTypeId), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-2", FindingSeverity.Low, "Title", "desc", DateTime.UtcNow, fileId, reviewTypeId), TestContext.CancellationToken);

            FindingQueryResult filtered = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = repositoryId,
                Severity = FindingSeverity.High,
                ReviewTypeId = reviewTypeId,
                SortField = FindingSortField.Severity,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 100
            }, TestContext.CancellationToken);

            Assert.AreEqual(1, filtered.TotalCount);
            Assert.HasCount(1, filtered.Items);
            Assert.AreEqual("fp-1", filtered.Items[0].Fingerprint);
        }

        [TestMethod]
        public async Task FindingRepository_QueryAsync_SearchesTitleOrDescription()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);

            await findings.AddAsync(CreateFinding(repositoryId, "fp-1", FindingSeverity.Medium, "Null reference", "A potential null dereference", DateTime.UtcNow), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(repositoryId, "fp-2", FindingSeverity.Medium, "Unused variable", "Minor cleanup", DateTime.UtcNow), TestContext.CancellationToken);

            FindingQueryResult result = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = repositoryId,
                SearchText = "null",
                SortField = FindingSortField.CreatedDate,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 100
            }, TestContext.CancellationToken);

            Assert.AreEqual(1, result.TotalCount);
            Assert.HasCount(1, result.Items);
            Assert.AreEqual("Null reference", result.Items[0].Title);
        }

        [TestMethod]
        public async Task FindingRepository_QueryAsync_PaginatesAndCounts()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            DateTime baseDate = DateTime.UtcNow;

            for (int index = 0; index < 5; index++)
            {
                await findings.AddAsync(CreateFinding(repositoryId, $"fp-{index}", FindingSeverity.Medium, $"Issue {index}", "desc", baseDate.AddMinutes(index)), TestContext.CancellationToken);
            }

            FindingQueryResult firstPage = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = repositoryId,
                SortField = FindingSortField.CreatedDate,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 2
            }, TestContext.CancellationToken);

            FindingQueryResult lastPage = await findings.QueryAsync(new FindingQuery
            {
                RepositoryId = repositoryId,
                SortField = FindingSortField.CreatedDate,
                SortDescending = true,
                PageIndex = 2,
                PageSize = 2
            }, TestContext.CancellationToken);

            Assert.AreEqual(5, firstPage.TotalCount);
            Assert.HasCount(2, firstPage.Items);
            Assert.AreEqual("fp-4", firstPage.Items[0].Fingerprint);
            Assert.AreEqual("fp-3", firstPage.Items[1].Fingerprint);

            Assert.AreEqual(5, lastPage.TotalCount);
            Assert.HasCount(1, lastPage.Items);
            Assert.AreEqual("fp-0", lastPage.Items[0].Fingerprint);
        }

        [TestMethod]
        public async Task FindingRepository_GetNavigationAsync_RespectsOrderingAndFilters()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"), TestContext.CancellationToken);
            DateTime baseDate = DateTime.UtcNow;

            long firstId = await findings.AddAsync(CreateFinding(repositoryId, "fp-low", FindingSeverity.Low, "Low", "desc", baseDate.AddMinutes(1)), TestContext.CancellationToken);
            long middleId = await findings.AddAsync(CreateFinding(repositoryId, "fp-medium", FindingSeverity.Medium, "Medium", "desc", baseDate.AddMinutes(2)), TestContext.CancellationToken);
            long lastId = await findings.AddAsync(CreateFinding(repositoryId, "fp-high", FindingSeverity.High, "High", "desc", baseDate.AddMinutes(3)), TestContext.CancellationToken);

            FindingNavigation navigation = await findings.GetNavigationAsync(middleId, new FindingQuery
            {
                RepositoryId = repositoryId,
                SortField = FindingSortField.Severity,
                SortDescending = true,
                PageIndex = 0,
                PageSize = 100
            }, TestContext.CancellationToken);

            Assert.AreEqual(lastId, navigation.PreviousId);
            Assert.AreEqual(firstId, navigation.NextId);
        }

        [TestMethod]
        public async Task FindingRepository_CountByRepositoryAsync_GroupsCounts()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long firstRepositoryId = await repositories.AddAsync(CreateRepository("First"), TestContext.CancellationToken);
            long secondRepositoryId = await repositories.AddAsync(CreateRepository("Second"), TestContext.CancellationToken);

            await findings.AddAsync(CreateFinding(firstRepositoryId, "fp-1"), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(firstRepositoryId, "fp-2"), TestContext.CancellationToken);
            await findings.AddAsync(CreateFinding(secondRepositoryId, "fp-3"), TestContext.CancellationToken);

            IReadOnlyList<RepositoryIssueCount> counts = await findings.CountByRepositoryAsync(TestContext.CancellationToken);

            Assert.HasCount(2, counts);
            Assert.AreEqual(2, counts.Single(count => count.RepositoryId == firstRepositoryId).Count);
            Assert.AreEqual(1, counts.Single(count => count.RepositoryId == secondRepositoryId).Count);
        }

        private static Finding CreateFinding(
            long repositoryId,
            string fingerprint,
            FindingSeverity severity = FindingSeverity.Medium,
            string title = "Title",
            string description = "Description",
            DateTime? createdDate = null,
            long? repoFileId = null,
            long? reviewTypeId = null)
        {
            DateTime now = createdDate ?? DateTime.UtcNow;

            return new Finding
            {
                RepositoryId = repositoryId,
                RepoFileId = repoFileId,
                ReviewTypeId = reviewTypeId,
                Fingerprint = fingerprint,
                Severity = severity,
                Title = title,
                Description = description,
                Status = FindingStatus.New,
                Category = FindingCategory.CodeIssue,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private static Repository CreateRepository(string name)
        {
            DateTime now = DateTime.UtcNow;

            return new Repository
            {
                Name = name,
                LocalPath = $"C:\\src\\{name}",
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private static RepoFile CreateRepoFile(long repositoryId, string relativePath)
        {
            return new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = relativePath,
                FileName = Path.GetFileName(relativePath),
                Extension = Path.GetExtension(relativePath).TrimStart('.'),
                Sha256 = Guid.NewGuid().ToString("N"),
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            };
        }

        private static ReviewType CreateReviewType(long repositoryId, string name)
        {
            DateTime now = DateTime.UtcNow;

            return new ReviewType
            {
                RepositoryId = repositoryId,
                Name = name,
                FileExtensions = "cs",
                DefaultPriority = 10,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private static QueueItem CreateQueueItem(long repositoryId, long repoFileId, long reviewTypeId, int priority)
        {
            return new QueueItem
            {
                RepositoryId = repositoryId,
                RepoFileId = repoFileId,
                ReviewTypeId = reviewTypeId,
                Priority = priority,
                CreatedDate = DateTime.UtcNow
            };
        }

        private sealed class TestDatabase : IDisposable
        {
            private readonly string _directory;

            public TestDatabase()
            {
                _directory = Path.Combine(Path.GetTempPath(), "revathryn-data-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);

                string databasePath = Path.Combine(_directory, "test.db");
                Options = new DatabaseOptions(new Contracts.RevathrynSettings("test")) { ConnectionString = $"Data Source={databasePath};Pooling=False" };
                Factory = new SqliteConnectionFactory(Options);

                SqliteDatabaseInitializer initializer = new(Factory);
                initializer.Initialize();
            }

            public DatabaseOptions Options { get; }

            public SqliteConnectionFactory Factory { get; }

            public void Dispose()
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }
        }

        public TestContext TestContext { get; set; }

        private static readonly string[] expected = ["cs", "json"];
        private static readonly string[] expectedArray = ["docs"];
    }
}
