using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Review
{
    /// <summary>
    /// Structured differences between an original detection and a prompt test run. A different result is not
    /// automatically a better result; this comparison is descriptive only.
    /// </summary>
    public sealed class PromptTestComparison
    {
        /// <summary>True when the original detection produced an issue.</summary>
        public bool OriginalIssueDetected { get; set; }

        /// <summary>True when the revised prompt produced at least one issue.</summary>
        public bool TestIssueDetected { get; set; }

        /// <summary>Number of issues produced by the revised prompt.</summary>
        public int TestIssueCount { get; set; }

        /// <summary>Severity of the original issue.</summary>
        public FindingSeverity? OriginalSeverity { get; set; }

        /// <summary>Highest severity produced by the revised prompt.</summary>
        public FindingSeverity? TestHighestSeverity { get; set; }

        /// <summary>True when a test result appears to correspond to the original issue.</summary>
        public bool MatchesOriginalIssue { get; set; }

        /// <summary>Context keys the revised execution still requested.</summary>
        public List<string> ContextRequests { get; set; } = [];

        /// <summary>Descriptive notes, including configuration differences and reproducibility limitations.</summary>
        public List<string> Notes { get; set; } = [];
    }
}
