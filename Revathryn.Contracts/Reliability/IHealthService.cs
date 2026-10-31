namespace Revathryn.Contracts.Reliability
{
    /// <summary>Evaluates the operational health of Revathryn.</summary>
    public interface IHealthService
    {
        /// <summary>Evaluates readiness: whether the application can perform work.</summary>
        Task<HealthReport> GetReadinessAsync(CancellationToken cancellationToken = default);

        /// <summary>Evaluates liveness: whether the application is running.</summary>
        HealthReport GetLiveness();
    }
}
