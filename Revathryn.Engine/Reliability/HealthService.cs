using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Evaluates the operational health of Revathryn.</summary>
    public sealed class HealthService : IHealthService
    {
        private readonly IDatabaseContext _databaseContext;
        private readonly IQueueRepository _queueRepository;
        private readonly IAIEngineRepository _aiEngineRepository;
        private readonly IFindingRepository _findingRepository;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IProcessingGate _processingGate;

        /// <summary>Initialises a new health service.</summary>
        public HealthService(IDatabaseContext databaseContext, IQueueRepository queueRepository, IAIEngineRepository aiEngineRepository, IFindingRepository findingRepository, IRepositoryRepository repositoryRepository, IProcessingGate processingGate)
        {
            _databaseContext = databaseContext;
            _queueRepository = queueRepository;
            _aiEngineRepository = aiEngineRepository;
            _findingRepository = findingRepository;
            _repositoryRepository = repositoryRepository;
            _processingGate = processingGate;
        }

        /// <summary>Evaluates readiness: whether the application can perform work.</summary>
        public async Task<HealthReport> GetReadinessAsync(CancellationToken cancellationToken = default)
        {
            List<HealthCheckResult> checks =
            [
                await CheckDatabaseConnectivityAsync(cancellationToken).ConfigureAwait(false),
                await CheckBackgroundProcessingAsync(cancellationToken).ConfigureAwait(false),
                await CheckAnalyserAvailabilityAsync(cancellationToken).ConfigureAwait(false),
                await CheckRepositoryAnalyserAsync(cancellationToken).ConfigureAwait(false),
                await CheckReviewProcessingAsync(cancellationToken).ConfigureAwait(false),
                CheckProcessingPause()
            ];

            return new HealthReport
            {
                Status = ComputeOverallStatus(checks),
                Checks = checks,
                EvaluatedUtc = DateTime.UtcNow
            };
        }

        /// <summary>Evaluates liveness: whether the application is running.</summary>
        public HealthReport GetLiveness()
        {
            return new HealthReport
            {
                Status = HealthStatus.Healthy,
                Checks = [CreateCheck(LivenessCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthLivenessHealthy)],
                EvaluatedUtc = DateTime.UtcNow
            };
        }

        private async Task<HealthCheckResult> CheckDatabaseConnectivityAsync(CancellationToken cancellationToken)
        {
            bool canConnect = await _databaseContext.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            if (canConnect)
            {
                return CreateCheck(DatabaseConnectivityCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthDatabaseConnectivityHealthy);
            }

            return CreateCheck(DatabaseConnectivityCheck, HealthStatus.Unavailable, null, Strings.HealthDatabaseConnectivityUnavailable);
        }

        private async Task<HealthCheckResult> CheckBackgroundProcessingAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _queueRepository.CountAsync(cancellationToken).ConfigureAwait(false);
                return CreateCheck(BackgroundProcessingCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthBackgroundProcessingHealthy);
            }
            catch (Exception ex)
            {
                return CreateCheck(BackgroundProcessingCheck, HealthStatus.Unavailable, null, string.Format(Strings.HealthBackgroundProcessingUnavailable, ex.Message));
            }
        }

        private async Task<HealthCheckResult> CheckAnalyserAvailabilityAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<AIEngine> enabled = await _aiEngineRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);

                if (enabled.Count == 0)
                {
                    return CreateCheck(AnalyserAvailabilityCheck, HealthStatus.Degraded, null, Strings.HealthAnalyserAvailabilityDegraded);
                }

                return CreateCheck(AnalyserAvailabilityCheck, HealthStatus.Healthy, DateTime.UtcNow, string.Format(Strings.HealthAnalyserAvailabilityHealthy, enabled.Count));
            }
            catch (Exception ex)
            {
                return CreateCheck(AnalyserAvailabilityCheck, HealthStatus.Unavailable, null, string.Format(Strings.HealthAnalyserAvailabilityUnavailable, ex.Message));
            }
        }

        private async Task<HealthCheckResult> CheckRepositoryAnalyserAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<Repository> repositories = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                IReadOnlyList<AIEngine> engines = await _aiEngineRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

                Dictionary<long, AIEngine> enginesById = [];
                foreach (AIEngine engine in engines)
                {
                    enginesById[engine.Id] = engine;
                }

                List<string> problems = [];
                foreach (Repository repository in repositories)
                {
                    if (!repository.AIEngineId.HasValue)
                    {
                        continue;
                    }

                    if (!enginesById.TryGetValue(repository.AIEngineId.Value, out AIEngine engine))
                    {
                        problems.Add(string.Format(Strings.HealthRepositoryAnalyserMissing, repository.Name));
                    }
                    else if (!engine.Enabled)
                    {
                        problems.Add(string.Format(Strings.HealthRepositoryAnalyserDisabled, repository.Name, engine.Name));
                    }
                }

                if (problems.Count > 0)
                {
                    return CreateCheck(RepositoryAnalyserCheck, HealthStatus.Degraded, null, string.Join(ListSeparator, problems));
                }

                return CreateCheck(RepositoryAnalyserCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthRepositoryAnalyserHealthy);
            }
            catch (Exception ex)
            {
                return CreateCheck(RepositoryAnalyserCheck, HealthStatus.Unavailable, null, string.Format(Strings.HealthRepositoryAnalyserUnavailable, ex.Message));
            }
        }

        private async Task<HealthCheckResult> CheckReviewProcessingAsync(CancellationToken cancellationToken)
        {
            try
            {
                QueueMetrics metrics = await _queueRepository.GetMetricsAsync(cancellationToken).ConfigureAwait(false);
                int operationalFailureCount = await _findingRepository.CountOutstandingOperationalFailuresAsync(cancellationToken).ConfigureAwait(false);

                if (operationalFailureCount > 0 || metrics.RetryCount > 0)
                {
                    return CreateCheck(ReviewProcessingCheck, HealthStatus.Degraded, null, string.Format(Strings.HealthReviewProcessingDegraded, operationalFailureCount, metrics.RetryCount));
                }

                return CreateCheck(ReviewProcessingCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthReviewProcessingHealthy);
            }
            catch (Exception ex)
            {
                return CreateCheck(ReviewProcessingCheck, HealthStatus.Unavailable, null, string.Format(Strings.HealthReviewProcessingUnavailable, ex.Message));
            }
        }

        private HealthCheckResult CheckProcessingPause()
        {
            if (_processingGate.IsPaused)
            {
                return CreateCheck(ProcessingPauseCheck, HealthStatus.Unavailable, null, string.Format(Strings.HealthProcessingPaused, _processingGate.Reason));
            }

            return CreateCheck(ProcessingPauseCheck, HealthStatus.Healthy, DateTime.UtcNow, Strings.HealthProcessingActive);
        }

        private static HealthCheckResult CreateCheck(string name, HealthStatus status, DateTime? lastSuccessfulUtc, string detail)
        {
            return new HealthCheckResult
            {
                Name = name,
                Status = status,
                LastSuccessfulUtc = lastSuccessfulUtc,
                Detail = detail
            };
        }

        private static HealthStatus ComputeOverallStatus(IReadOnlyList<HealthCheckResult> checks)
        {
            HealthStatus worst = HealthStatus.Healthy;
            foreach (HealthCheckResult check in checks)
            {
                if (check.Status > worst)
                {
                    worst = check.Status;
                }
            }

            return worst;
        }
    }
}
