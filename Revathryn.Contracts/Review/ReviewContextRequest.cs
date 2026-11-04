namespace Revathryn.Contracts.Review
{
    /// <summary>Request for additional context about a file, e.g. symbol information.</summary>
    public class ReviewContextRequest
    {
        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Owning repository name, used to route per-repository analysis artefacts.</summary>
        public string RepositoryName { get; set; } = string.Empty;

        /// <summary>The file for which context is requested.</summary>
        public long RepoFileId { get; set; }

        /// <summary>Language of the file.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Identifies the kind of context requested, e.g. "symbols" or "dependencies".</summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>Maximum number of context items to return.</summary>
        public int MaxItems { get; set; }

        /// <summary>Absolute path of the file context is requested for.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Free-text query used when <see cref="ContextKey"/> is "search".</summary>
        public string Query { get; set; } = string.Empty;
    }
}
