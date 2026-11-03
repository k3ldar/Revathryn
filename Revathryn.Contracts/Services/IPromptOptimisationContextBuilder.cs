using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Assembles the analysis and proposal prompts for a prompt optimisation from the historical detection,
    /// validation and repository context. It always uses the prompt snapshots stored with the original detection.
    /// </summary>
    public interface IPromptOptimisationContextBuilder
    {
        /// <summary>Builds the prompt optimisation context for an optimisation record.</summary>
        Task<PromptOptimisationContext> BuildAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the stage 2 proposal user prompt from the completed analysis and the complete original prompts.
        /// </summary>
        Task<string> BuildProposalUserPromptAsync(PromptOptimisation optimisation, PromptAnalysisResult analysis, CancellationToken cancellationToken = default);
    }
}
