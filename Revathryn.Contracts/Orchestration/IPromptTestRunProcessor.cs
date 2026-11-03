namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Executes queued isolated prompt test runs.</summary>
    public interface IPromptTestRunProcessor
    {
        /// <summary>
        /// Claims and executes the next due test run.
        /// Returns false when nothing was due, true when a test run was processed.
        /// </summary>
        Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    }
}
