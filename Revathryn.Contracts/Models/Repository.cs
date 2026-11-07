namespace Revathryn.Contracts.Models
{
    /// <summary>A registered local repository under review.</summary>
    public class Repository
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Human readable repository name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of the repository.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Local folder path containing the repository source.</summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>Remote URI reserved for future integrations. Not populated yet.</summary>
        public string RepositoryUri { get; set; } = string.Empty;

        /// <summary>Whether the repository participates in processing.</summary>
        public bool Enabled { get; set; }

        /// <summary>Whether file changes should automatically enqueue reviews.</summary>
        public bool AutomaticReviewEnabled { get; set; }

        /// <summary>Maximum number of context requests permitted per review.</summary>
        public int MaxContextRequests { get; set; }

        /// <summary>Identifier of the AI engine used for this repository's reviews, or null to use the system default.</summary>
        public long? AIEngineId { get; set; }

        /// <summary>Repository-specific scanning configuration; defaults apply when null.</summary>
        public RepositoryScanOptions ScanOptions { get; set; }

        /// <summary>UTC timestamp of the last successful scan, or null when the repository has never been scanned.</summary>
        public DateTime? LastSuccessfulScanDate { get; set; }

        /// <summary>UTC timestamp when the repository was registered.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>UTC timestamp when the repository was last modified.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
