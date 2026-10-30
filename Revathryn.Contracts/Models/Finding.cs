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

        /// <summary>Lifecycle status of the finding.</summary>
        public FindingStatus Status { get; set; }

        /// <summary>Distinguishes code issues from operational failures.</summary>
        public FindingCategory Category { get; set; }

        /// <summary>UTC timestamp when the finding was created.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>UTC timestamp when the finding was last modified.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
