namespace Revathryn.Contracts.Models
{
    /// <summary>A global review type combined with its enabled state for a repository.</summary>
    public sealed class RepositoryReviewType
    {
        /// <summary>The global review type.</summary>
        public ReviewType ReviewType { get; set; } = new();

        /// <summary>Whether the review type is enabled for the repository.</summary>
        public bool Enabled { get; set; }
    }
}
