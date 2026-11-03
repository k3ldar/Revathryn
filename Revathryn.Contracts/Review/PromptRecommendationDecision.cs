namespace Revathryn.Contracts.Review
{
    /// <summary>A recommendation decision for one prompt type produced by the disagreement analysis.</summary>
    public sealed class PromptRecommendationDecision
    {
        /// <summary>Whether the review engine recommends changing this prompt.</summary>
        public bool Required { get; set; }

        /// <summary>Why the prompt should or should not change.</summary>
        public string Explanation { get; set; } = string.Empty;
    }
}
