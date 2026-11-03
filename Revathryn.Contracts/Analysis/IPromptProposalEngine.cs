using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>
    /// Asks the independent review engine to generate improved prompt text based on a completed disagreement
    /// analysis. The result contains metadata for user review; it never activates a prompt.
    /// </summary>
    public interface IPromptProposalEngine
    {
        /// <summary>
        /// Requests and parses one or two prompt recommendations. Provider and parsing failures are reported in
        /// the result rather than thrown.
        /// </summary>
        Task<PromptProposalEngineResult> GenerateAsync(PromptProposalRequest request, CancellationToken cancellationToken = default);
    }
}
