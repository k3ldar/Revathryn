namespace Revathryn.Contracts.Models
{
    /// <summary>Cached descriptive structure summary for a single file fingerprint.</summary>
    public sealed class RepoFileArchitecture
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The summarised file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>SHA256 fingerprint the summary was produced against.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Free-text structural summary produced by the model.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Display name of the engine that produced the summary.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Model identifier that produced the summary.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Version of the analysis pipeline that produced the summary.</summary>
        public string AnalysisVersion { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the summary was produced.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
