using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Requests and queries independent issue validations.</summary>
    public interface IIssueValidationService
    {
        /// <summary>
        /// Queues a validation for the finding. When one is already pending or running, that validation
        /// is returned and no duplicate is created.
        /// </summary>
        Task<IssueValidationRequestResult> RequestAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queues an automatic validation for the finding on behalf of the re-review pipeline. When one is
        /// already pending or running, that validation is returned and no duplicate is created.
        /// </summary>
        Task<IssueValidationRequestResult> RequestAutomaticAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Queues a new validation that retries a failed one. The failed record is left intact.</summary>
        Task<IssueValidationRequestResult> RetryAsync(long validationId, CancellationToken cancellationToken = default);

        /// <summary>Returns a validation by id, or null.</summary>
        Task<IssueValidation> GetByIdAsync(long validationId, CancellationToken cancellationToken = default);

        /// <summary>Returns all validations for a finding, newest first.</summary>
        Task<IReadOnlyList<IssueValidation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent validation for a finding, regardless of status, or null.</summary>
        Task<IssueValidation> GetLatestAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the pending or running validation for a finding, or null.</summary>
        Task<IssueValidation> GetActiveAsync(long findingId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recent completed validation for each of the supplied findings.</summary>
        Task<IReadOnlyDictionary<long, IssueValidation>> GetLatestCompletedAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken = default);
    }
}
