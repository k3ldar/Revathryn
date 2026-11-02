namespace Revathryn.Models.Issues
{
    /// <summary>Repository selection page for issue browsing.</summary>
    public sealed class IssueRepositoryListViewModel
    {
        /// <summary>Registered repositories with their issue counts.</summary>
        public IReadOnlyList<IssueRepositoryListItemViewModel> Repositories { get; set; } = [];
    }
}
