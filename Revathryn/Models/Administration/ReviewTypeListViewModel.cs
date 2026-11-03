using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>List of review types for the administration page.</summary>
    public sealed class ReviewTypeListViewModel
    {
        /// <summary>Review types paired with their owning repository names.</summary>
        public IReadOnlyList<ReviewTypeListItemViewModel> Items { get; set; } = [];

        /// <summary>Repositories available for filtering.</summary>
        public IReadOnlyList<Repository> Repositories { get; set; } = [];

        /// <summary>Currently selected repository filter, or null for all repositories.</summary>
        public long? SelectedRepositoryId { get; set; }
    }
}
