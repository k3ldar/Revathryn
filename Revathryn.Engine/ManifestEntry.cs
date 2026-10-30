using Revathryn.Contracts;

namespace Revathryn.Engine
{
    public class ManifestEntry : IManifestEntry
    {
        /// <summary>Full path of the source file that was analysed.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Language identifier as reported by the analyser, e.g. "CSharp".</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Language specification version used during analysis, e.g. "14.0".</summary>
        public string LanguageVersion { get; set; } = string.Empty;

        /// <summary>
        /// The analyser version that produced this entry.
        /// If the current analyser version is higher, the file will be re-analysed.
        /// </summary>
        public int AnalyserVersion { get; set; }

        /// <summary>Relative or absolute path of the generated symbol JSON file.</summary>
        public string OutputFile { get; set; } = string.Empty;

        /// <summary>SHA256 hash of the source file contents at time of analysis.</summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>LastWriteTimeUtc of the source file at time of analysis, used as a cheap pre-filter.</summary>
        public DateTime SourceLastWriteUtc { get; set; }

        /// <summary>UTC timestamp when this entry was written.</summary>
        public DateTime AnalysedUtc { get; set; }
    }
}
