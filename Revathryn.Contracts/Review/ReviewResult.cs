using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Review
{
    /// <summary>A single finding produced by a review execution.</summary>
    public class ReviewResult
    {
        /// <summary>Stable fingerprint used for duplicate detection.</summary>
        public string Fingerprint { get; set; } = string.Empty;

        /// <summary>Severity of the finding.</summary>
        public FindingSeverity Severity { get; set; }

        /// <summary>Short title of the finding.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the finding.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Optional suggested fix.</summary>
        public string SuggestedFix { get; set; } = string.Empty;

        /// <summary>Optional stable identifier emitted by the engine, used to correlate findings across reviews.</summary>
        public string FindingKey { get; set; } = string.Empty;

        /// <summary>Starting line of the finding, when reported by the engine.</summary>
        public int? LineStart { get; set; }

        /// <summary>Ending line of the finding, when reported by the engine.</summary>
        public int? LineEnd { get; set; }
    }
}
