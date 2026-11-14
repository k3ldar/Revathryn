namespace Revathryn.Contracts.Models
{
    /// <summary>Links a global review type to a repository and stores its enabled state.</summary>
    public sealed class RepositoryReview
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The linked repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The linked review type.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>Whether the review type is enabled for the repository.</summary>
        public bool Enabled { get; set; }
    }
}
