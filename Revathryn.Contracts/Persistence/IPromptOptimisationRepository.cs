using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for prompt optimisation analysis and recommendation records.</summary>
    public interface IPromptOptimisationRepository
    {
        /// <summary>
        /// Inserts a pending optimisation. Returns 0 without inserting when the validation already has
        /// a pending or running optimisation.
        /// </summary>
        Task<long> TryAddAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default);

        /// <summary>Returns an optimisation by id, or null when not found.</summary>
        Task<PromptOptimisation> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all optimisations for a finding, newest first.</summary>
        Task<IReadOnlyList<PromptOptimisation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the pending or running optimisation for a validation, or null.</summary>
        Task<PromptOptimisation> GetActiveByValidationAsync(long issueValidationId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent completed optimisation for a finding, or null.</summary>
        Task<PromptOptimisation> GetLatestCompletedAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims the next due optimisation under a lease, reclaiming any whose lease expired.
        /// Returns null when nothing is due.
        /// </summary>
        Task<PromptOptimisation> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

        /// <summary>Records the engine and prompt snapshot about to be analysed.</summary>
        Task SaveExecutionSnapshotAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default);

        /// <summary>Persists the parsed disagreement analysis from stage 1.</summary>
        Task RecordAnalysisAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default);

        /// <summary>Persists the generated recommendations from stage 2 and marks the optimisation completed.</summary>
        Task RecordProposalsAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default);

        /// <summary>Marks the optimisation failed.</summary>
        Task FailAsync(long id, string errorMessage, FailureCategory category, string rawResponse, TokenUsage usage, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Returns the optimisation to the pending state for a later retry.</summary>
        Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string errorMessage, string rawResponse, TokenUsage usage, CancellationToken cancellationToken = default);
    }
}
