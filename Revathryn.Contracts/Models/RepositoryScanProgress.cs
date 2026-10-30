using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>Progress information emitted while a repository is being scanned.</summary>
    public sealed class RepositoryScanProgress
    {
        /// <summary>The repository being scanned.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The current scan phase.</summary>
        public ScanPhase Phase { get; set; }

        /// <summary>Number of files discovered so far.</summary>
        public int FilesDiscovered { get; set; }

        /// <summary>Number of files processed so far.</summary>
        public int FilesProcessed { get; set; }

        /// <summary>Number of previously known files removed during reconciliation.</summary>
        public int FilesRemoved { get; set; }

        /// <summary>UTC timestamp when the progress was reported.</summary>
        public DateTime ReportedUtc { get; set; }
    }
}
