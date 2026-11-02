using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>Describes exactly which context was supplied to the review engine for a validation.</summary>
    public sealed class IssueValidationContextReference
    {
        /// <summary>Where the source context originated.</summary>
        public IssueValidationContextMode Mode { get; set; }

        /// <summary>The original detection context that was reused, when one existed.</summary>
        public long? ReviewContextId { get; set; }

        /// <summary>Repository-relative path of the primary source file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>SHA256 of the source supplied to the review engine.</summary>
        public string SuppliedSha256 { get; set; } = string.Empty;

        /// <summary>SHA256 of the source as it was when the issue was detected.</summary>
        public string OriginalSha256 { get; set; } = string.Empty;

        /// <summary>True when the current repository source differs from what the detection engine saw.</summary>
        public bool SourceChangedSinceDetection { get; set; }

        /// <summary>Number of additional context items supplied.</summary>
        public int ContextItemCount { get; set; }

        /// <summary>Number of supplied context items that were truncated during detection.</summary>
        public int TruncatedContextItemCount { get; set; }

        /// <summary>Context keys the detection engine requested.</summary>
        public List<string> RequestedContextKeys { get; set; } = [];

        /// <summary>Human readable notes about gaps or limitations in the supplied context.</summary>
        public List<string> Notes { get; set; } = [];
    }
}
