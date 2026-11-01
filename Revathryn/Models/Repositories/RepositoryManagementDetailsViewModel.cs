using Revathryn.Contracts.Models;

namespace Revathryn.Models.Repositories
{
    /// <summary>Management detail view for a single repository.</summary>
    public sealed class RepositoryManagementDetailsViewModel
    {
        /// <summary>The registered repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>Scan records for the repository, most recent first.</summary>
        public IReadOnlyList<RepositoryScanRecord> Scans { get; set; } = [];

        /// <summary>Review definitions belonging to the repository.</summary>
        public IReadOnlyList<ReviewType> ReviewTypes { get; set; } = [];

        /// <summary>Whether the configured local path can no longer be reached.</summary>
        public bool PathMissing { get; set; }

        /// <summary>The most recent scan record, or null when none exists.</summary>
        public RepositoryScanRecord LastScan { get; set; }
    }
}
