namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregated summary of outstanding issues for the dashboard.</summary>
    public sealed class IssueSummary
    {
        /// <summary>Total number of outstanding issues.</summary>
        public int TotalCount { get; set; }

        /// <summary>Outstanding issue counts grouped by severity.</summary>
        public IReadOnlyList<IssueSeverityCount> BySeverity { get; set; } = [];

        /// <summary>Outstanding issue counts grouped by repository.</summary>
        public IReadOnlyList<IssueRepositoryCount> ByRepository { get; set; } = [];

        /// <summary>The most recently identified findings.</summary>
        public IReadOnlyList<Finding> RecentlyIdentified { get; set; } = [];

        /// <summary>The most recently updated findings.</summary>
        public IReadOnlyList<Finding> RecentlyUpdated { get; set; } = [];
    }
}
