using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Aggregates persisted application data for the operational dashboard.</summary>
    public sealed class DashboardQueryService : IDashboardQueryService
    {
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepositoryScanRepository _scanRepository;
        private readonly IQueueRepository _queueRepository;
        private readonly IIssueQueryService _issueQueryService;
        private readonly IHealthService _healthService;
        private readonly UptimeModel _uptimeModel;

        /// <summary>Initialises a new dashboard query service.</summary>
        public DashboardQueryService(
            IRepositoryRepository repositoryRepository,
            IRepositoryScanRepository scanRepository,
            IQueueRepository queueRepository,
            IIssueQueryService issueQueryService,
            IHealthService healthService,
            UptimeModel uptimeModel)
        {
            _repositoryRepository = repositoryRepository;
            _scanRepository = scanRepository;
            _queueRepository = queueRepository;
            _issueQueryService = issueQueryService;
            _healthService = healthService;
            _uptimeModel = uptimeModel;
        }

        /// <summary>Returns the aggregated dashboard snapshot.</summary>
        public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Repository> repositories = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<RepositoryScanRecord> scans = await _scanRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            Dictionary<long, Repository> repositoriesById = [];
            foreach (Repository repository in repositories)
            {
                repositoriesById[repository.Id] = repository;
            }

            List<RepositoryDashboardItem> repositoryItems = [];
            int discoveredCount = 0;
            DateTime? lastRepositoryScanUtc = null;

            foreach (Repository repository in repositories)
            {
                RepositoryScanRecord lastScan = scans.FirstOrDefault(scan => scan.RepositoryId == repository.Id);

                if (repository.LastSuccessfulScanDate.HasValue)
                {
                    discoveredCount++;

                    if (!lastRepositoryScanUtc.HasValue || repository.LastSuccessfulScanDate.Value > lastRepositoryScanUtc.Value)
                    {
                        lastRepositoryScanUtc = repository.LastSuccessfulScanDate.Value;
                    }
                }

                repositoryItems.Add(new RepositoryDashboardItem
                {
                    Repository = repository,
                    LastScan = lastScan,
                    PathMissing = !Directory.Exists(repository.LocalPath)
                });
            }

            IReadOnlyList<RecentScanItem> recentScans = BuildRecentScans(scans, repositoriesById);
            IssueSummary issueSummary = await _issueQueryService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
            HealthReport healthReport = await _healthService.GetReadinessAsync(cancellationToken).ConfigureAwait(false);
            int queuedAnalysisCount = await _queueRepository.CountAsync(cancellationToken).ConfigureAwait(false);

            return new DashboardSnapshot
            {
                Repositories = repositoryItems,
                ConfiguredRepositoryCount = repositories.Count,
                DiscoveredRepositoryCount = discoveredCount,
                LastRepositoryScanUtc = lastRepositoryScanUtc,
                QueuedAnalysisCount = queuedAnalysisCount,
                IssueSummary = issueSummary,
                HealthReport = healthReport,
                StartTimeUtc = _uptimeModel.StartTime,
                Uptime = _uptimeModel.Duration,
                RecentScans = recentScans
            };
        }

        private static IReadOnlyList<RecentScanItem> BuildRecentScans(IReadOnlyList<RepositoryScanRecord> scans, Dictionary<long, Repository> repositoriesById)
        {
            List<RecentScanItem> items = [];
            HashSet<long> seenRepositoryIds = [];

            foreach (RepositoryScanRecord scan in scans)
            {
                if (!seenRepositoryIds.Add(scan.RepositoryId))
                {
                    continue;
                }

                repositoriesById.TryGetValue(scan.RepositoryId, out Repository repository);
                items.Add(new RecentScanItem
                {
                    RepositoryId = scan.RepositoryId,
                    RepositoryName = repository?.Name ?? scan.RepositoryId.ToString(),
                    Scan = scan
                });

                if (items.Count == DefaultRecentScanCount)
                {
                    break;
                }
            }

            return items;
        }
    }
}
