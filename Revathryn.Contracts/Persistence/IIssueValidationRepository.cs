using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for independent issue validations. Records are historical and never overwritten.</summary>
    public interface IIssueValidationRepository
    {
        /// <summary>
        /// Inserts a pending validation. Returns 0 without inserting when the finding already has
        /// a pending or running validation.
        /// </summary>
        Task<long> TryAddAsync(IssueValidation validation, CancellationToken cancellationToken = default);

        /// <summary>Returns a validation by id, or null.</summary>
        Task<IssueValidation> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all validations for a finding, newest first.</summary>
        Task<IReadOnlyList<IssueValidation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent validation for a finding, regardless of status, or null.</summary>
        Task<IssueValidation> GetLatestAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the pending or running validation for a finding, or null.</summary>
        Task<IssueValidation> GetActiveByFindingAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent completed validation for each finding, as a summary projection.</summary>
        Task<IReadOnlyDictionary<long, IssueValidation>> GetLatestCompletedByFindingsAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims the next due validation under a lease, reclaiming any whose lease expired.
        /// Returns null when nothing is due.
        /// </summary>
        Task<IssueValidation> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

        /// <summary>Records what is about to be sent to the review engine: engine, prompts and context.</summary>
        Task SaveExecutionSnapshotAsync(IssueValidation validation, CancellationToken cancellationToken = default);

        /// <summary>Records the verdict and marks the validation completed.</summary>
        Task CompleteAsync(IssueValidation validation, CancellationToken cancellationToken = default);

        /// <summary>Marks the validation failed. No verdict is recorded.</summary>
        Task FailAsync(long id, string errorMessage, FailureCategory category, string rawResponse, TokenUsage usage, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Returns the validation to the pending state for a later retry.</summary>
        Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string errorMessage, string rawResponse, TokenUsage usage, CancellationToken cancellationToken = default);
    }
}
