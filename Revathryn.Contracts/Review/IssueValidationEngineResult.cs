using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>The outcome of asking the review engine to validate a finding.</summary>
    public sealed class IssueValidationEngineResult
    {
        /// <summary>True when the review engine produced a valid, parsed assessment.</summary>
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

        /// <summary>The conclusion reached.</summary>
        public IssueValidationVerdict Verdict { get; set; }

        /// <summary>Normalised confidence between 0 and 1, or null when not supplied.</summary>
        public double? Confidence { get; set; }

        /// <summary>Short conclusion.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Full explanation.</summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>Recommended severity, when supplied.</summary>
        public FindingSeverity? RecommendedSeverity { get; set; }

        /// <summary>Explanation of the severity recommendation.</summary>
        public string SeverityReasoning { get; set; } = string.Empty;

        /// <summary>Evidence that references supplied source.</summary>
        public List<IssueValidationEvidence> Evidence { get; set; } = [];

        /// <summary>Suggested corrections to the original finding.</summary>
        public List<string> Suggestions { get; set; } = [];

        /// <summary>Limitations and missing context.</summary>
        public List<string> Limitations { get; set; } = [];
    }
}
