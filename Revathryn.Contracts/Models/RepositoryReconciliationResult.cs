namespace Revathryn.Contracts.Models
{
    /// <summary>Counts describing how a repository inventory changed during reconciliation.</summary>
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
    }
}
