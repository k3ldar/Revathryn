using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>The outcome of a single chat completion request.</summary>
    public sealed class AICompletionResult
    {
        /// <summary>Whether content was returned.</summary>
        public bool Success { get; set; }

        /// <summary>The assistant message content when successful.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>The raw provider response body, retained for diagnostics.</summary>
        public string RawBody { get; set; } = string.Empty;

        /// <summary>The failure classification when unsuccessful.</summary>
        public AICompletionFailureKind FailureKind { get; set; }

        /// <summary>A message that is safe to display and never contains credentials.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>The delay requested by the provider before retrying, when supplied.</summary>
        public TimeSpan? RetryAfter { get; set; }

        /// <summary>Token usage reported by the provider.</summary>
        public TokenUsage Usage { get; set; } = new();
    }
}
