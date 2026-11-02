using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Services
{
    /// <summary>Assembles the prompts and context supplied to the review engine for a finding.</summary>
    public interface IIssueValidationContextBuilder
    {
        /// <summary>Builds the validation prompts, reusing the original detection context where available.</summary>
        Task<IssueValidationContext> BuildAsync(Finding finding, CancellationToken cancellationToken = default);
    }
}
