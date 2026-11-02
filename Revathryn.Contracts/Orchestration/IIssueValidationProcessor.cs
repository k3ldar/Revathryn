namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Executes queued issue validations.</summary>
    public interface IIssueValidationProcessor
    {
        /// <summary>
        /// Claims and executes the next due validation.
        /// Returns false when nothing was due, true when a validation was processed.
        /// </summary>
        Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    }
}
