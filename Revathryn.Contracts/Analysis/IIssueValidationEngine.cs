using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>Independently validates a finding using the configured review engine.</summary>
    public interface IIssueValidationEngine
    {
        /// <summary>
        /// Requests and parses an independent assessment. Provider and parsing failures are reported
        /// in the result rather than thrown, and are never converted into a verdict.
        /// </summary>
        Task<IssueValidationEngineResult> ValidateAsync(IssueValidationRequest request, CancellationToken cancellationToken = default);
    }
}
