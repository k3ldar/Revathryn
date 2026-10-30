namespace Revathryn.Contracts.Models
{
    /// <summary>A persisted record of a single repository scan execution.</summary>
    public class RepositoryScanRecord
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>UTC timestamp when the scan started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC timestamp when the scan completed.</summary>
        public DateTime CompletedUtc { get; set; }

        /// <summary>Whether the scan completed successfully.</summary>
        public bool Succeeded { get; set; }

        /// <summary>Number of files discovered during the scan.</summary>
        public int FilesDiscovered { get; set; }

        /// <summary>Number of files not previously registered.</summary>
        public int NewFiles { get; set; }

        /// <summary>Number of files whose content fingerprint changed.</summary>
        public int ChangedFiles { get; set; }

        /// <summary>Number of files whose content fingerprint was unchanged.</summary>
        public int UnchangedFiles { get; set; }

        /// <summary>Number of previously present files removed from the repository.</summary>
        public int RemovedFiles { get; set; }
    }
}
