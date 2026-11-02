namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregate issue count for a single repository.</summary>
    public sealed class RepositoryIssueCount
    {
        /// <summary>Repository identifier.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Total number of issues belonging to the repository.</summary>
        public int Count { get; set; }
    }
}
