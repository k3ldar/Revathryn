using Revathryn.Contracts;

namespace Revathryn.Contracts.Models
{
    /// <summary>Configuration for Jaccard-based duplicate detection of findings.</summary>
    public sealed class IssueSimilarityOptions
    {
        /// <summary>Whether background duplicate detection is active.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Minimum Jaccard similarity, from 0.0 to 1.0, for a finding to be marked duplicate.</summary>
        public double Threshold { get; set; } = Consts.DefaultIssueSimilarityThreshold;

        /// <summary>Minimum token length considered when tokenising finding text.</summary>
        public int MinimumTokenLength { get; set; } = Consts.DefaultIssueSimilarityMinimumTokenLength;
    }
}
