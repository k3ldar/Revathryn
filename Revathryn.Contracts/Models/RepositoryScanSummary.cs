namespace Revathryn.Contracts.Models
{
    /// <summary>Summary of a completed repository scan operation.</summary>
    public sealed class RepositoryScanSummary
    {
        /// <summary>The repository that was scanned.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Number of files discovered during the scan.</summary>
        public int FilesDiscovered { get; set; }

        /// <summary>Number of files not previously registered.</summary>
        public int NewFiles { get; set; }

        /// <summary>Number of files whose content fingerprint changed.</summary>
        public int ChangedFiles { get; set; }

        /// <summary>Number of files whose content fingerprint was unchanged.</summary>
        public int UnchangedFiles { get; set; }

        /// <summary>Number of previously registered files removed from the repository.</summary>
        public int RemovedFiles { get; set; }

        /// <summary>Number of files reliably detected as renamed or moved.</summary>
        public int RenamedFiles { get; set; }

        /// <summary>Structured per-file change details produced by reconciliation.</summary>
        public IReadOnlyList<RepositoryFileChange> Changes { get; set; } = new List<RepositoryFileChange>();

        /// <summary>Number of C# files that were analysed during this scan.</summary>
        public int CSharpFilesAnalysed { get; set; }

        /// <summary>Number of C# files that failed analysis during this scan.</summary>
        public int CSharpAnalysisFailures { get; set; }

        /// <summary>Number of review queue entries added during this scan.</summary>
        public int QueueEntriesAdded { get; set; }

        /// <summary>Number of review queue insertions skipped because they were already pending.</summary>
        public int QueueDuplicatesSkipped { get; set; }

        /// <summary>UTC timestamp when the scan started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC timestamp when the scan completed.</summary>
        public DateTime CompletedUtc { get; set; }
    }
}
