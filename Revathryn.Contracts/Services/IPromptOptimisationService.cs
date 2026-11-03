using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Requests and queries guided prompt optimisation analyses initiated from a completed issue validation.
    /// The review engine investigates the disagreement and recommends prompt improvements; it never activates them.
    /// </summary>
    public interface IPromptOptimisationService
    {
        /// <summary>
        /// Queues a prompt optimisation for the finding. When one is already pending or running for the same
        /// validation, that optimisation is returned and no duplicate is created.
        /// </summary>
        Task<PromptOptimisationStartResult> RequestAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Queues a new optimisation that retries a failed one. The failed record is left intact.</summary>
        Task<PromptOptimisationStartResult> RetryAsync(long optimisationId, CancellationToken cancellationToken = default);

        /// <summary>Returns an optimisation by id, or null when not found.</summary>
        Task<PromptOptimisation> GetByIdAsync(long optimisationId, CancellationToken cancellationToken = default);

        /// <summary>Returns all optimisations for a finding, newest first.</summary>
        Task<IReadOnlyList<PromptOptimisation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent completed optimisation for a finding, or null.</summary>
        Task<PromptOptimisation> GetLatestCompletedAsync(long findingId, CancellationToken cancellationToken = default);
    }
}
