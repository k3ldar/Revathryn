using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>A review type paired with its owning repository name.</summary>
    public sealed class ReviewTypeListItemViewModel
    {
        /// <summary>The review type.</summary>
        public ReviewType ReviewType { get; set; } = new();

        /// <summary>Name of the owning repository.</summary>
        public string RepositoryName { get; set; } = string.Empty;
    }
}
