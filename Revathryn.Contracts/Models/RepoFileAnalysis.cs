namespace Revathryn.Contracts.Models
{
    /// <summary>Records that a review type completed against a specific file fingerprint.</summary>
    public class RepoFileAnalysis
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The analysed file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type that completed.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>SHA256 fingerprint the analysis was performed against.</summary>
        public string Sha256 { get; set; } = String.Empty;

        /// <summary>UTC timestamp of the last completed analysis.</summary>
        public DateTime LastAnalysedDate { get; set; }

        /// <summary>Optional version of the analysis engine that produced the result.</summary>
        public string AnalysisVersion { get; set; } = String.Empty;

        /// <summary>Version of the review definition the analysis completed against.</summary>
        public string ReviewDefinitionVersion { get; set; } = String.Empty;
    }
}
