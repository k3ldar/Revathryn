using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>Request to the review engine to generate improved prompt text.</summary>
    public sealed class PromptProposalRequest
    {
        /// <summary>The review engine configuration to use.</summary>
        public ReviewEngineSettings Settings { get; set; }

        /// <summary>The dedicated proposal generation system prompt.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt containing the analysis and the complete original prompts.</summary>
        public string UserPrompt { get; set; } = string.Empty;
    }
}
