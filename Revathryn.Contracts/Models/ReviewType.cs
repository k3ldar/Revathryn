namespace Revathryn.Contracts.Models
{
    /// <summary>A global review definition shared by all repositories.</summary>
    public class ReviewType
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Stable identifier used to seed built-in review definitions.</summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>Display name of the review definition.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Optional description of what the review targets.</summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>Comma-separated list of file extensions this review applies to.</summary>
        public string FileExtensions { get; set; } = String.Empty;

        /// <summary>The review-specific prompt combined with the master prompt at execution time.</summary>
        public string Prompt { get; set; } = String.Empty;

        /// <summary>Default queue priority; higher values are processed first.</summary>
        public int DefaultPriority { get; set; }

        /// <summary>Fingerprint of the review-defining configuration; changes trigger re-review.</summary>
        public string ReviewDefinitionVersion { get; set; } = String.Empty;

        /// <summary>UTC timestamp when the review definition was created.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>UTC timestamp when the review definition was last modified.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
