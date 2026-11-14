using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>List of global review types for the administration page.</summary>
    public sealed class ReviewTypeListViewModel
    {
        /// <summary>Global review types.</summary>
        public IReadOnlyList<ReviewTypeListItemViewModel> Items { get; set; } = [];
    }
}
