namespace Revathryn.Contracts.Models
{
    /// <summary>Persisted record of a failed architecture pass, including its full context window.</summary>
    public sealed class ArchitectureAnalysisFailure
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The file that failed to summarise. Null when the file has since been removed.</summary>
        public long? RepoFileId { get; set; }

        /// <summary>Repository-relative path of the file at failure time.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Language of the file at failure time.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>SHA256 fingerprint of the file content at failure time.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>System prompt sent to the model.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>File content sent as the user turn.</summary>
        public string FileContent { get; set; } = string.Empty;

        /// <summary>Error message describing the failure.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Raw provider response body, when one was returned.</summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>Display name of the engine that failed.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Model identifier that failed.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the failure was recorded.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
