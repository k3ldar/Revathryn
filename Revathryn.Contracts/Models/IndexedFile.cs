namespace Revathryn.Contracts.Models
{
    /// <summary>Filesystem and analysis state for a file held in the per-repository symbol index.</summary>
    public sealed class IndexedFile
    {
        /// <summary>Absolute path of the source file.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>SHA-256 hash of the source content.</summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>Last write time of the source file in UTC.</summary>
        public DateTime SourceLastWriteUtc { get; set; }

        /// <summary>Time the analysis completed in UTC.</summary>
        public DateTime AnalysedUtc { get; set; }

        /// <summary>Version of the analyser that produced the symbols.</summary>
        public int AnalyserVersion { get; set; }

        /// <summary>Language of the file.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Language version used for analysis.</summary>
        public string LanguageVersion { get; set; } = string.Empty;
    }
}
