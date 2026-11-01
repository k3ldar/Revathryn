
using static Revathryn.Contracts.Consts;

namespace Revathryn.Review
{
    /// <summary>Configuration for the review engines.</summary>
    public class ReviewOptions
    {
        public ReviewOptions()
        {
            AnalysisOutputDirectory = Path.Combine(AppContext.BaseDirectory, PathAnalysisOutput);
            ManifestPath = Path.Combine(AnalysisOutputDirectory, FileAnalysisManifest);
        }
        /// <summary>Directory where analysis artefacts are written.</summary>
        public string AnalysisOutputDirectory { get; set; }

        /// <summary>Path of the file-backed analysis manifest.</summary>
        public string ManifestPath { get; set; } = FileAnalysisManifest;
    }
}
