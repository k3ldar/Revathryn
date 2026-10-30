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
    }
}
