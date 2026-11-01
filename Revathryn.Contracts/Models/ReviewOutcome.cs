namespace Revathryn.Contracts.Models
{
    /// <summary>The complete outcome of a review, persisted atomically.</summary>
    public sealed class ReviewOutcome
    {
        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The reviewed file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type that completed.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>SHA256 fingerprint the review was performed against.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Analysis engine version that produced the result.</summary>
        public string AnalysisVersion { get; set; } = string.Empty;

        /// <summary>Review definition version the review completed against.</summary>
        public string ReviewDefinitionVersion { get; set; } = string.Empty;

        /// <summary>Findings produced by the review.</summary>
        public IReadOnlyList<Finding> Findings { get; set; } = [];

        /// <summary>Per-invocation usage records accumulated while processing the queue item.</summary>
        public IReadOnlyList<ReviewUsage> UsageRecords { get; set; } = [];

        /// <summary>UTC timestamp of the completed review.</summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    }
}
