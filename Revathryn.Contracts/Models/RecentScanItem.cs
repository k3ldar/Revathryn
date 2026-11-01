namespace Revathryn.Contracts.Models
{
    /// <summary>A scan record paired with its repository display name.</summary>
    public sealed class RecentScanItem
    {
        /// <summary>The repository identifier.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Display name of the repository.</summary>
        public string RepositoryName { get; set; } = string.Empty;

        /// <summary>The scan record.</summary>
        public RepositoryScanRecord Scan { get; set; } = new();
    }
}
