using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>A global review type for the administration list.</summary>
    public sealed class ReviewTypeListItemViewModel
    {
        /// <summary>The review type.</summary>
        public ReviewType ReviewType { get; set; } = new();
    }
}
