namespace Revathryn.Contracts.Review
{
    /// <summary>
    /// The prompts assembled for a prompt optimisation. The analysis user prompt contains the original detection,
    /// independent validation, repository context and prompt metadata supplied to the review engine.
    /// </summary>
    public sealed class PromptOptimisationContext
    {
        /// <summary>The dedicated prompt analysis system prompt.</summary>
        public string AnalysisSystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt containing the historical finding, validation, repository context and prompt metadata.</summary>
        public string AnalysisUserPrompt { get; set; } = string.Empty;

        /// <summary>The dedicated prompt proposal generation system prompt.</summary>
        public string ProposalSystemPrompt { get; set; } = string.Empty;
    }
}
