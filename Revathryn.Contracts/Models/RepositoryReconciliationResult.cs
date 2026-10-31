namespace Revathryn.Contracts.Models
{
    /// <summary>Counts and per-file details describing how a repository inventory changed during reconciliation.</summary>
    public sealed class RepositoryReconciliationResult
    {
        /// <summary>Number of files not previously registered.</summary>
        public int NewFiles { get; set; }

        /// <summary>Number of files whose content fingerprint changed.</summary>
        public int ChangedFiles { get; set; }

        /// <summary>Number of files whose content fingerprint was unchanged.</summary>
        public int UnchangedFiles { get; set; }

        /// <summary>Number of previously present files removed from the repository.</summary>
        public int RemovedFiles { get; set; }

        /// <summary>Number of files reliably detected as renamed or moved.</summary>
        public int RenamedFiles { get; set; }

        /// <summary>Structured per-file change details for subsequent phases.</summary>
        public IReadOnlyList<RepositoryFileChange> Changes { get; set; } = [];
    }
}
