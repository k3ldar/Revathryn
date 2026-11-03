namespace Revathryn.Contracts.Review
{
    /// <summary>
    /// An AI-generated prompt recommendation with the metadata required to review, edit and version it.
    /// The proposed prompt is never activated automatically.
    /// </summary>
    public sealed class PromptRecommendation
    {
        /// <summary>The exact prompt used during detection.</summary>
        public string OriginalPrompt { get; set; } = string.Empty;

        /// <summary>The AI-generated alternative.</summary>
        public string ProposedPrompt { get; set; } = string.Empty;

        /// <summary>Why the change was recommended.</summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>What has actually changed.</summary>
        public string ChangesSummary { get; set; } = string.Empty;

        /// <summary>Anticipated improvement.</summary>
        public string ExpectedBenefit { get; set; } = string.Empty;

        /// <summary>Possible unintended consequences.</summary>
        public List<string> PotentialDrawbacks { get; set; } = [];

        /// <summary>Reference to the analysis that generated this recommendation.</summary>
        public long? SourceAnalysisId { get; set; }
    }
}
