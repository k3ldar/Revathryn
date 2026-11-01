namespace Revathryn.Contracts.Models
{
    /// <summary>Number of outstanding issues for a single repository.</summary>
    public sealed class IssueRepositoryCount
    {
        /// <summary>The repository identifier.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Display name of the repository.</summary>
        public string RepositoryName { get; set; } = string.Empty;

        /// <summary>Number of outstanding issues in the repository.</summary>
        public int Count { get; set; }
    }
}
