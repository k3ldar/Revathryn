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

        /// <summary>Context seeded up front, e.g. related symbol usage hints, included in the original review message.</summary>
        public List<string> InitialContext { get; set; } = [];

        /// <summary>Requested context rounds, each supplied to the AI as a separate follow-up message.</summary>
        public List<ReviewContextRound> ContextRounds { get; set; } = [];

        /// <summary>Pre-computed descriptive structure summary priming the review. Empty when unavailable.</summary>
        public string ArchitectureSummary { get; set; } = string.Empty;
    }
}
