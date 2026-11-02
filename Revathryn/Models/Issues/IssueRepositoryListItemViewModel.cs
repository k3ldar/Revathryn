using Revathryn.Contracts.Models;

namespace Revathryn.Models.Issues
{
    /// <summary>A repository shown on the issue browsing selection page.</summary>
    public sealed class IssueRepositoryListItemViewModel
    {
        /// <summary>The registered repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>Total number of issues identified for the repository.</summary>
        public int IssueCount { get; set; }
    }
}
