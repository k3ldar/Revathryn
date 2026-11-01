using Revathryn.Contracts.Models;

namespace Revathryn.Models.Repositories
{
    /// <summary>Operational state of a single repository on the management page.</summary>
    public sealed class RepositoryListItemViewModel
    {
        /// <summary>The registered repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>The most recent scan record, or null when never scanned.</summary>
        public RepositoryScanRecord LastScan { get; set; }

        /// <summary>Whether the configured local path can no longer be reached.</summary>
        public bool PathMissing { get; set; }

        /// <summary>Whether the repository has a discovery or configuration problem.</summary>
        public bool HasProblems { get; set; }
    }
}
