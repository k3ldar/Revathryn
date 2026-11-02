namespace Revathryn.Contracts.Models
{
    /// <summary>The original context supplied to the AI during a single review execution.</summary>
    public sealed class AIReviewContext
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The reviewed file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type that produced the execution.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>SHA256 fingerprint of the file content at review time.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Language of the reviewed file.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the reviewed file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>The complete instructions supplied to the AI.</summary>
        public string ReviewInstructions { get; set; } = string.Empty;

        /// <summary>The reviewed file content supplied to the AI.</summary>
        public string FileContent { get; set; } = string.Empty;

        /// <summary>Additional context items supplied to the AI, preserved as supplied.</summary>
        public IReadOnlyList<ReviewContextItem> ContextItems { get; set; } = [];

        /// <summary>UTC timestamp when the review was completed.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
