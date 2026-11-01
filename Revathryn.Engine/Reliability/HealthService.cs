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

        /// <summary>Initialises a new health service.</summary>
        public HealthService(IDatabaseContext databaseContext, IQueueRepository queueRepository, IAIEngineRepository aiEngineRepository)
        {
            _databaseContext = databaseContext;
            _queueRepository = queueRepository;
            _aiEngineRepository = aiEngineRepository;
        }

        /// <summary>Evaluates readiness: whether the application can perform work.</summary>
        public async Task<HealthReport> GetReadinessAsync(CancellationToken cancellationToken = default)
        {
            List<HealthCheckResult> checks =
            [
                await CheckDatabaseConnectivityAsync(cancellationToken).ConfigureAwait(false),
                await CheckBackgroundProcessingAsync(cancellationToken).ConfigureAwait(false),
                await CheckAnalyserAvailabilityAsync(cancellationToken).ConfigureAwait(false)
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
