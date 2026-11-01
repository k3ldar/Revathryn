using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>Outcome of a review execution.</summary>
    public class ReviewResponse
    {
        /// <summary>Whether the review completed without an operational error.</summary>
        public bool Success { get; set; }

        /// <summary>Optional error message when <see cref="Success"/> is false.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Identifier of the engine that served the review.</summary>
        public long EngineId { get; set; }

        /// <summary>Display name of the engine that served the review.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Model identifier used by the engine.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Token usage accumulated across the internal correction attempts.</summary>
        public TokenUsage Usage { get; set; }

        /// <summary>Findings produced by the review. Empty when no issues were identified.</summary>
        public List<ReviewResult> Results { get; set; } = [];

        /// <summary>
        /// Context keys the model requested before it can produce final results.
        /// When non-empty, the caller should retrieve the requested context and retry.
        /// </summary>
        public List<string> ContextRequests { get; set; } = [];
    }
}
