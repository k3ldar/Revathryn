namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Executes queued duplicate-detection work items.</summary>
    public interface IDuplicateDetectionProcessor
    {
        /// <summary>
        /// Claims and executes the next due duplicate-detection item.
        /// Returns false when nothing was due, true when an item was processed.
        /// </summary>
        Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    }
}
