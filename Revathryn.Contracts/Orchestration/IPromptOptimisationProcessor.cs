namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Executes queued prompt optimisation analyses and proposal generations.</summary>
    public interface IPromptOptimisationProcessor
    {
        /// <summary>
        /// Claims and executes the next due optimisation.
        /// Returns false when nothing was due, true when an optimisation was processed.
        /// </summary>
        Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    }
}
