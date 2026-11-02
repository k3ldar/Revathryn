using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>A single chat completion request sent to an AI provider.</summary>
    public sealed class AICompletionRequest
    {
        /// <summary>The engine configuration to use.</summary>
        public ReviewEngineSettings Settings { get; set; }

        /// <summary>The system prompt.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt.</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>Whether the provider should be asked to return a JSON object.</summary>
        public bool RequireJson { get; set; } = true;
    }
}
