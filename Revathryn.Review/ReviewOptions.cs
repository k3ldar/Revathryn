
using Revathryn.Contracts;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Review
{
    /// <summary>Configuration for the review engines.</summary>
    public class ReviewOptions
    {
        public ReviewOptions(RevathrynSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));

            AnalysisOutputDirectory = Path.Combine(settings.BaseSettingsPath, PathAnalysisOutput);
        }

        /// <summary>Directory where analysis artefacts are written.</summary>
        public string AnalysisOutputDirectory { get; set; }
    }
}
