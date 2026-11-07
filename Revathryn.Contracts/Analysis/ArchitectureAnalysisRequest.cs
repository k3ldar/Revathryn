namespace Revathryn.Contracts.Analysis
{
    /// <summary>Request to produce a descriptive structural summary of a single file.</summary>
    public sealed class ArchitectureAnalysisRequest
    {
        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The file being summarised.</summary>
        public long RepoFileId { get; set; }

        /// <summary>Language of the file under review, e.g. "CSharp".</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>File contents to summarise.</summary>
        public string FileContent { get; set; } = string.Empty;

        /// <summary>Instructions framing the descriptive structure pass.</summary>
        public string Prompt { get; set; } = string.Empty;
    }
}
