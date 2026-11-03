using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>
    /// Asks the independent review engine to investigate a detection disagreement and produce a structured
    /// analysis. The engine recommends whether either prompt should change; it never approves or activates its
    /// own recommendations.
    /// </summary>
    public interface IPromptAnalysisEngine
    {
        /// <summary>
        /// Requests and parses a disagreement analysis. Provider and parsing failures are reported in the result
        /// rather than thrown, and are never converted into a recommendation.
        /// </summary>
        Task<PromptAnalysisEngineResult> AnalyseAsync(PromptAnalysisRequest request, CancellationToken cancellationToken = default);
    }
}
