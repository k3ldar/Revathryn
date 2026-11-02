using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// Configuration of the independent review engine used to validate issues.
    /// This is deliberately separate from the detection engines held in <see cref="AIEngine"/>.
    /// </summary>
    public sealed class ReviewEngineSettings
    {
        /// <summary>The AI provider flavour.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Base endpoint of the OpenAI-compatible API.</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>Model identifier.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Sampling temperature, or null to omit it for models that do not support it.</summary>
        public double? Temperature { get; set; }

        /// <summary>Reasoning effort, or empty to omit it for models that do not support it.</summary>
        public string ReasoningEffort { get; set; } = string.Empty;

        /// <summary>Maximum number of output tokens.</summary>
        public int MaxTokens { get; set; }

        /// <summary>
        /// Name of the secret holding the API key. The key itself is resolved at execution time
        /// and is never stored in this model, shown in the UI or returned by the API.
        /// </summary>
        public string SecretKeyName { get; set; } = string.Empty;

        /// <summary>Whether the review engine is available.</summary>
        public bool Enabled { get; set; }

        /// <summary>Provider-specific options as a JSON object merged into the request payload.</summary>
        public string AdditionalOptions { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the settings were last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }
    }
}
