using System.Diagnostics;
using System.Threading;

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
        private readonly IFindingRepository _findingRepository;
        private readonly IReviewUsageRepository _reviewUsageRepository;
        private readonly UptimeModel _uptimeModel;

        /// <summary>Initialises a new dashboard query service.</summary>
        public DashboardQueryService(
            IRepositoryRepository repositoryRepository,
            IRepositoryScanRepository scanRepository,
            IQueueRepository queueRepository,
            IIssueQueryService issueQueryService,
            IHealthService healthService,
            IFindingRepository findingRepository,
            IReviewUsageRepository reviewUsageRepository,
            UptimeModel uptimeModel)
        {
            _repositoryRepository = repositoryRepository;
            _scanRepository = scanRepository;
            _queueRepository = queueRepository;
            _issueQueryService = issueQueryService;
            _healthService = healthService;
            _findingRepository = findingRepository;
            _reviewUsageRepository = reviewUsageRepository;
            _uptimeModel = uptimeModel;
        }

        /// <summary>Returns the aggregated dashboard snapshot.</summary>
        public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Repository> repositories = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<RepositoryScanRecord> scans = await _scanRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<QueueItem> pendingItems = await _queueRepository.GetPendingAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<long, int> queuedByRepository = BuildQueuedCountsByRepository(pendingItems);

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

                queuedByRepository.TryGetValue(repository.Id, out int queuedCount);

                repositoryItems.Add(new RepositoryDashboardItem
                {
                    Repository = repository,
                    LastScan = lastScan,
                    PathMissing = !Directory.Exists(repository.LocalPath),
                    QueuedAnalysisCount = queuedCount
                });
            }

            IReadOnlyList<RecentScanItem> recentScans = BuildRecentScans(scans, repositoriesById);
            IssueSummary issueSummary = await _issueQueryService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
            HealthReport healthReport = await _healthService.GetReadinessAsync(cancellationToken).ConfigureAwait(false);
            QueueMetrics queueMetrics = await _queueRepository.GetMetricsAsync(cancellationToken).ConfigureAwait(false);
            int failedAnalysisCount = await _findingRepository.CountOutstandingOperationalFailuresAsync(cancellationToken).ConfigureAwait(false);
            ReviewUsageSummary reviewUsage = await _reviewUsageRepository.GetSummaryAsync(cancellationToken).ConfigureAwait(false);

            return new DashboardSnapshot
            {
                Repositories = repositoryItems,
                ConfiguredRepositoryCount = repositories.Count,
                DiscoveredRepositoryCount = discoveredCount,
                LastRepositoryScanUtc = lastRepositoryScanUtc,
                QueuedAnalysisCount = queueMetrics.PendingCount,
                QueueMetrics = queueMetrics,
                FailedAnalysisCount = failedAnalysisCount,
                IssueSummary = issueSummary,
                HealthReport = healthReport,
                StartTimeUtc = _uptimeModel.StartTime,
                Uptime = _uptimeModel.Duration,
                ResourceMetrics = CaptureResourceMetrics(),
                ReviewUsage = reviewUsage,
                RecentScans = recentScans
            };
        }

        private SystemResourceMetrics CaptureResourceMetrics()
        {
            using Process process = Process.GetCurrentProcess();
            GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();

            return new SystemResourceMetrics
            {
                ManagedHeapBytes = GC.GetTotalMemory(false),
                GcCommittedBytes = gcMemoryInfo.TotalCommittedBytes,
                TotalAllocatedBytes = GC.GetTotalAllocatedBytes(true),
                WorkingSetBytes = process.WorkingSet64,
                PrivateMemoryBytes = process.PrivateMemorySize64,
                ThreadPoolThreadCount = ThreadPool.ThreadCount,
                ThreadPoolPendingWorkItemCount = ThreadPool.PendingWorkItemCount,
                CpuUsagePercent = CalculateCpuUsagePercent(process.TotalProcessorTime)
            };
        }

        private double CalculateCpuUsagePercent(TimeSpan processCpuTime)
        {
            double elapsedMilliseconds = _uptimeModel.Duration.TotalMilliseconds;

            if (elapsedMilliseconds <= 0)
            {
                return 0;
            }

            return processCpuTime.TotalMilliseconds * CpuUsagePercentageScale / elapsedMilliseconds;
        }

        private static Dictionary<long, int> BuildQueuedCountsByRepository(IReadOnlyList<QueueItem> pendingItems)
        {
            Dictionary<long, int> counts = [];

            foreach (QueueItem item in pendingItems)
            {
                counts.TryGetValue(item.RepositoryId, out int currentCount);
                counts[item.RepositoryId] = currentCount + 1;
            }

            return counts;
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
