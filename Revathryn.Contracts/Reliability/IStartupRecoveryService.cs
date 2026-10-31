namespace Revathryn.Contracts.Reliability
{
    /// <summary>Reconciles persisted queue state against completed analysis on startup.</summary>
    public interface IStartupRecoveryService
    {
        /// <summary>Reconciles the transient queue against persisted review outcomes.</summary>
        Task<RecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
    }
}
