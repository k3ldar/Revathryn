namespace Revathryn.Contracts.Review
{
    /// <summary>
    /// The structured disagreement analysis produced by the review engine. The review engine may recommend
    /// system prompt changes, review-specific prompt changes, both, or neither.
    /// </summary>
    public sealed class PromptAnalysisResult
    {
        /// <summary>Short conclusion of why the engines disagreed.</summary>
        public string AnalysisSummary { get; set; } = string.Empty;

        /// <summary>Identified causes of the disagreement.</summary>
        public List<string> DisagreementCause { get; set; } = [];

        /// <summary>System prompt change recommendation.</summary>
        public PromptRecommendationDecision SystemPromptRecommendation { get; set; } = new();

        /// <summary>Review-specific prompt change recommendation.</summary>
        public PromptRecommendationDecision ReviewPromptRecommendation { get; set; } = new();

        /// <summary>Additional observations that do not fit the structured fields.</summary>
        public List<string> AdditionalObservations { get; set; } = [];
    }
}
