using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>An issue identified by a review, including operational failures.</summary>
    public class Finding
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The file the finding relates to. Nullable so findings survive file removal.</summary>
        public long? RepoFileId { get; set; }

        /// <summary>The review type that produced the finding. Nullable so findings survive definition removal.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Optional reference to the original AI context supplied to the review that produced this finding.</summary>
        public long? ReviewContextId { get; set; }

        /// <summary>Stable fingerprint used for exact duplicate detection.</summary>
        public string Fingerprint { get; set; } = string.Empty;

        /// <summary>Id of the finding this finding duplicates, or null when not a duplicate.</summary>
        public long? DuplicateOfFindingId { get; set; }

        /// <summary>Jaccard similarity score recorded when this finding was marked duplicate.</summary>
        public double? DuplicateSimilarityScore { get; set; }

        /// <summary>Severity of the finding.</summary>
        public FindingSeverity Severity { get; set; }

        /// <summary>Short title of the finding.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the finding.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Optional suggested fix.</summary>
        public string SuggestedFix { get; set; } = string.Empty;

        /// <summary>Model identifier used by the engine that performed the review producing this finding.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Lifecycle status of the finding.</summary>
        public FindingStatus Status { get; set; }

        /// <summary>Distinguishes code issues from operational failures.</summary>
        public FindingCategory Category { get; set; }

        /// <summary>UTC timestamp when the finding was created.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>UTC timestamp when the finding was last modified.</summary>
        public DateTime LastModifiedDate { get; set; }

        /// <summary>Stable cross-review key used to correlate findings across reviews. Empty when not correlatable.</summary>
        public string FindingKey { get; set; } = string.Empty;

        /// <summary>Starting line of the finding, when reported by the engine.</summary>
        public int? LineStart { get; set; }

        /// <summary>Ending line of the finding, when reported by the engine.</summary>
        public int? LineEnd { get; set; }

        /// <summary>How the finding was resolved, when resolved.</summary>
        public FindingResolutionOrigin ResolutionOrigin { get; set; }

        /// <summary>UTC timestamp when the finding was resolved, when resolved.</summary>
        public DateTime? ResolvedUtc { get; set; }

        /// <summary>SHA256 of the reviewed file content the finding was resolved against.</summary>
        public string ResolvedReviewSha256 { get; set; } = string.Empty;

        /// <summary>Git branch the finding was produced against. Empty for legacy or detached HEAD records.</summary>
        public string Branch { get; set; } = string.Empty;
    }
}
