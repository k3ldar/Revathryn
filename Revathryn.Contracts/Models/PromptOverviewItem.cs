using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A summary row for the prompt management overview.</summary>
    public sealed class PromptOverviewItem
    {
        /// <summary>The prompt identity id.</summary>
        public long PromptId { get; set; }

        /// <summary>Whether this is the system prompt or a review-specific prompt.</summary>
        public PromptType PromptType { get; set; }

        /// <summary>The review type id, or null for the system prompt.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Review type display name, or null for the system prompt.</summary>
        public string ReviewTypeName { get; set; }

        /// <summary>The active version number, or null when no version is active.</summary>
        public int? ActiveVersionNumber { get; set; }

        /// <summary>The active version id, or null when no version is active.</summary>
        public long? ActiveVersionId { get; set; }

        /// <summary>UTC timestamp when the prompt identity was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>Number of versions for this prompt.</summary>
        public int VersionCount { get; set; }
    }
}
