using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>Request to the review engine to investigate a detection disagreement.</summary>
    public sealed class PromptAnalysisRequest
    {
        /// <summary>The review engine configuration to use.</summary>
        public ReviewEngineSettings Settings { get; set; }

        /// <summary>The dedicated prompt analysis system prompt.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt containing the original finding, validation, repository context and prompt metadata.</summary>
        public string UserPrompt { get; set; } = string.Empty;
    }
}
