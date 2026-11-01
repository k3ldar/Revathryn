namespace Revathryn.Contracts.Models
{
    /// <summary>Dashboard state for a single configured repository.</summary>
    public sealed class RepositoryDashboardItem
    {
        /// <summary>The configured repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>The most recent scan record, or null when the repository has never been scanned.</summary>
        public RepositoryScanRecord LastScan { get; set; }

        /// <summary>Whether the configured local path can no longer be reached.</summary>
        public bool PathMissing { get; set; }

        /// <summary>Whether the repository has a discovery or configuration problem.</summary>
        public bool HasProblems => PathMissing || (LastScan is not null && !LastScan.Succeeded);
    }
}
