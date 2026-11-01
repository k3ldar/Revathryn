using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Engine;

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

            public Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }

            public Task<QueueItem> DequeueAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<QueueItem>(null);
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
    }
}
