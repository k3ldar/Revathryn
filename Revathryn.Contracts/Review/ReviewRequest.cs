namespace Revathryn.Contracts.Review
{
    /// <summary>Request to execute a single review against a file.</summary>
    public class ReviewRequest
    {
        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The file under review.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type driving the request.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>Language of the file under review, e.g. "CSharp".</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>File contents to review.</summary>
        public string FileContent { get; set; } = string.Empty;

        /// <summary>Review-specific prompt (review type prompt), combined with the master prompt by the caller.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Optional additional context supplied to the AI engine.</summary>
        public List<string> AdditionalContext { get; set; } = [];
    }
}
