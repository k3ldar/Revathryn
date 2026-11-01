using Revathryn.Contracts.Models;

namespace Revathryn.Models.Repositories
{
    /// <summary>List of repositories displayed on the repository management page.</summary>
    public sealed class RepositoryListViewModel
    {
        /// <summary>Configured repositories with their current operational state.</summary>
        public IReadOnlyList<RepositoryListItemViewModel> Repositories { get; set; } = [];
    }
}
