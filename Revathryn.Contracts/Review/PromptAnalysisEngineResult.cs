using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>The outcome of asking the review engine to analyse a detection disagreement.</summary>
    public sealed class PromptAnalysisEngineResult
    {
        /// <summary>True when a valid, parsed analysis was produced.</summary>
        public bool Success { get; set; }

        /// <summary>The failure classification when unsuccessful.</summary>
        public AICompletionFailureKind FailureKind { get; set; }

        /// <summary>True when the output was received but could not be parsed or validated.</summary>
        public bool Malformed { get; set; }

        /// <summary>A message safe to display when unsuccessful.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Delay requested by the provider before retrying, when supplied.</summary>
        public TimeSpan? RetryAfter { get; set; }

        /// <summary>The raw provider response, retained for diagnostics.</summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>Token usage for the request.</summary>
        public TokenUsage Usage { get; set; } = new();

        /// <summary>The structured analysis when successful.</summary>
        public PromptAnalysisResult Analysis { get; set; }
    }
}
