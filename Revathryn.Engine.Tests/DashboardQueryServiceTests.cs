using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Engine;
using Revathryn.Engine.Reliability;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DashboardQueryServiceTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task GetSnapshotAsync_RecentScans_ContainsOnlyMostRecentScanPerRepository()
        {
            FakeRepositoryRepository repositories = new();
            repositories.Repositories.Add(new Repository { Id = 1, Name = "Repo One" });
            repositories.Repositories.Add(new Repository { Id = 2, Name = "Repo Two" });

            DateTime scanTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            RepositoryScanRecord mostRecentRepoOne = new()
            {
                Id = 1,
                RepositoryId = 1,
                StartedUtc = scanTime.AddDays(3),
                CompletedUtc = scanTime.AddDays(3).AddMinutes(1)
            };

            RepositoryScanRecord repoTwo = new()
            {
                Id = 2,
                RepositoryId = 2,
                StartedUtc = scanTime.AddDays(2),
                CompletedUtc = scanTime.AddDays(2).AddMinutes(1)
            };

            RepositoryScanRecord olderRepoOne = new()
            {
                Id = 3,
                RepositoryId = 1,
                StartedUtc = scanTime.AddDays(1),
                CompletedUtc = scanTime.AddDays(1).AddMinutes(1)
            };

            FakeRepositoryScanRepository scans = new();
            scans.Records.Add(mostRecentRepoOne);
            scans.Records.Add(repoTwo);
            scans.Records.Add(olderRepoOne);

            DashboardQueryService service = CreateService(repositories, scans);

            DashboardSnapshot snapshot = await service.GetSnapshotAsync(TestContext.CancellationToken);

            Assert.HasCount(2, snapshot.RecentScans);
            Assert.AreEqual(1, snapshot.RecentScans[0].RepositoryId);
            Assert.AreSame(mostRecentRepoOne, snapshot.RecentScans[0].Scan);
            Assert.AreEqual("Repo One", snapshot.RecentScans[0].RepositoryName);
            Assert.AreEqual(2, snapshot.RecentScans[1].RepositoryId);
            Assert.AreSame(repoTwo, snapshot.RecentScans[1].Scan);
        }

        [TestMethod]
        public async Task GetSnapshotAsync_RecentScans_LimitedToDefaultRecentScanCount()
        {
            FakeRepositoryRepository repositories = new();
            FakeRepositoryScanRepository scans = new();

            DateTime scanTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int repositoryId = 1; repositoryId <= Consts.DefaultRecentScanCount + 2; repositoryId++)
            {
                repositories.Repositories.Add(new Repository { Id = repositoryId, Name = $"Repo {repositoryId}" });

                scans.Records.Add(new RepositoryScanRecord
                {
                    Id = repositoryId,
                    RepositoryId = repositoryId,
                    StartedUtc = scanTime.AddDays(repositoryId),
                    CompletedUtc = scanTime.AddDays(repositoryId).AddMinutes(1)
                });
            }

            DashboardQueryService service = CreateService(repositories, scans);

            DashboardSnapshot snapshot = await service.GetSnapshotAsync(TestContext.CancellationToken);

            Assert.HasCount(Consts.DefaultRecentScanCount, snapshot.RecentScans);
        }

        [TestMethod]
        public async Task GetSnapshotAsync_RecentScans_EmptyWhenNoScans()
        {
            FakeRepositoryRepository repositories = new();
            FakeRepositoryScanRepository scans = new();

            DashboardQueryService service = CreateService(repositories, scans);

            DashboardSnapshot snapshot = await service.GetSnapshotAsync(TestContext.CancellationToken);

            Assert.HasCount(0, snapshot.RecentScans);
        }

        [TestMethod]
        public async Task GetSnapshotAsync_MapsQueueMetricsToSnapshot()
        {
            FakeRepositoryRepository repositories = new();
            FakeRepositoryScanRepository scans = new();
            FakeQueueRepository queue = new()
            {
                Metrics = new QueueMetrics
                {
                    PendingCount = 3,
                    RetryCount = 1,
                    OldestPendingUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };
            FakeFindingRepository findings = new() { OperationalFailureCount = 2 };

            DashboardQueryService service = new(
                repositories,
                scans,
                queue,
                new FakeIssueQueryService(),
                new FakeHealthService(),
                findings,
                new FakeReviewUsageRepository(),
                new UptimeModel());

            DashboardSnapshot snapshot = await service.GetSnapshotAsync(TestContext.CancellationToken);

            Assert.AreEqual(3, snapshot.QueuedAnalysisCount);
            Assert.IsNotNull(snapshot.QueueMetrics);
            Assert.AreEqual(3, snapshot.QueueMetrics.PendingCount);
            Assert.AreEqual(1, snapshot.QueueMetrics.RetryCount);
            Assert.AreEqual(2, snapshot.FailedAnalysisCount);
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), snapshot.QueueMetrics.OldestPendingUtc.Value);
        }

        [TestMethod]
        public async Task GetSnapshotAsync_MapsReviewUsageSummaryToSnapshot()
        {
            FakeRepositoryRepository repositories = new();
            FakeRepositoryScanRepository scans = new();
            FakeReviewUsageRepository usage = new()
            {
                Summary = new ReviewUsageSummary
                {
                    TotalReviewCount = 5,
                    TodayReviewCount = 2
                }
            };

            DashboardQueryService service = new(
                repositories,
                scans,
                new FakeQueueRepository(),
                new FakeIssueQueryService(),
                new FakeHealthService(),
                new FakeFindingRepository(),
                usage,
                new UptimeModel());

            DashboardSnapshot snapshot = await service.GetSnapshotAsync(TestContext.CancellationToken);

            Assert.IsNotNull(snapshot.ReviewUsage);
            Assert.AreEqual(5, snapshot.ReviewUsage.TotalReviewCount);
            Assert.AreEqual(2, snapshot.ReviewUsage.TodayReviewCount);
        }

        [TestMethod]
        public async Task HealthService_GetReadinessAsync_DegradedWhenRetriesOrFailedAnalysesExist()
        {
            FakeQueueRepository queue = new()
            {
                Metrics = new QueueMetrics
                {
                    PendingCount = 2,
                    RetryCount = 1
                }
            };

            FakeFindingRepository findings = new() { OperationalFailureCount = 1 };
            HealthService healthService = new(new FakeDatabaseContext(), queue, new FakeAIEngineRepository(new AIEngine { Enabled = true }), findings);

            HealthReport report = await healthService.GetReadinessAsync(TestContext.CancellationToken);

            HealthCheckResult reviewCheck = report.Checks.Single(check => check.Name == Consts.ReviewProcessingCheck);
            Assert.AreEqual(HealthStatus.Degraded, reviewCheck.Status);
        }

        private static DashboardQueryService CreateService(
            IRepositoryRepository repositories,
            IRepositoryScanRepository scans)
        {
            return new DashboardQueryService(
                repositories,
                scans,
                new FakeQueueRepository(),
                new FakeIssueQueryService(),
                new FakeHealthService(),
                new FakeFindingRepository(),
                new FakeReviewUsageRepository(),
                new UptimeModel());
        }

        private sealed class FakeRepositoryRepository : IRepositoryRepository
        {
            public List<Repository> Repositories { get; } = [];

            public Task<Repository> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Repositories.FirstOrDefault(repository => repository.Id == id));
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([.. Repositories]);
            }

            public Task<IReadOnlyList<Repository>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([.. Repositories.Where(repository => repository.Enabled)]);
            }

            public Task<long> AddAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                return Task.FromResult(repository.Id);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepositoryScanRepository : IRepositoryScanRepository
        {
            public List<RepositoryScanRecord> Records { get; } = [];

            public Task<long> AddAsync(RepositoryScanRecord record, CancellationToken cancellationToken = default)
            {
                record.Id = Records.Count + 1;
                Records.Add(record);
                return Task.FromResult(record.Id);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanRecord>>([.. Records.Where(record => record.RepositoryId == repositoryId)]);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanRecord>>([.. Records]);
            }
        }

        private sealed class FakeQueueRepository : IQueueRepository
        {
            public int Count { get; set; }

            public QueueMetrics Metrics { get; set; } = new();

            public Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }

            public Task<QueueItem> ClaimNextAsync(DateTime leasedUntilUtc, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<QueueItem>(null);
            }

            public Task ReleaseForRetryAsync(long id, int priority, DateTime nextAttemptUtc, string lastError, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task MarkCompletedAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<QueueItem>>([]);
            }

            public Task<IReadOnlyList<QueueItem>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<QueueItem>>([]);
            }

            public Task RemoveAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<int> CountAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Count);
            }

            public Task<QueueMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Metrics);
            }
        }

        private sealed class FakeIssueQueryService : IIssueQueryService
        {
            public IssueSummary Summary { get; set; } = new();

            public Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FindingQueryResult());
            }

            public Task<IssueSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Summary);
            }
        }

        private sealed class FakeFindingRepository : IFindingRepository
        {
            public int OperationalFailureCount { get; set; }

            public Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Finding>(null);
            }

            public Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task<Finding> GetByFingerprintAsync(long repositoryId, string fingerprint, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Finding>(null);
            }

            public Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FindingQueryResult());
            }

            public Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationalFailureCount);
            }

            public Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewUsageRepository : IReviewUsageRepository
        {
            public ReviewUsageSummary Summary { get; set; } = new();

            public IReadOnlyList<ReviewUsage> Records { get; set; } = [];

            public Task<long> AddAsync(ReviewUsage usage, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task<IReadOnlyList<ReviewUsage>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Records);
            }

            public Task<ReviewUsageSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Summary);
            }
        }

        private sealed class FakeHealthService : IHealthService
        {
            public HealthReport Report { get; set; } = new();

            public Task<HealthReport> GetReadinessAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Report);
            }

            public HealthReport GetLiveness()
            {
                return Report;
            }
        }

        private sealed class FakeDatabaseContext : IDatabaseContext
        {
            public ITransactionScope BeginTransaction()
            {
                return new FakeTransactionScope();
            }

            public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }
        }

        private sealed class FakeTransactionScope : ITransactionScope
        {
            public void Commit()
            {
            }

            public void Rollback()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeAIEngineRepository : IAIEngineRepository
        {
            private readonly IReadOnlyList<AIEngine> _enabled;

            public FakeAIEngineRepository(params AIEngine[] enabled)
            {
                _enabled = enabled;
            }

            public Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AIEngine>(null);
            }

            public Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_enabled);
            }

            public Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_enabled);
            }

            public Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<AIEngine>(null);
            }

            public Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
