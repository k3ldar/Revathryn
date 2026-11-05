using Revathryn.Contracts;

namespace Revathryn.Contracts.Models
{
    /// <summary>Global platform settings, including AI and issue-management configuration.</summary>
    public sealed class AISettings
    {
        /// <summary>The configurable global master prompt prepended to every review prompt.</summary>
        public string MasterPrompt { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the settings were last modified.</summary>
        public DateTime LastModifiedDate { get; set; }

        /// <summary>Whether background duplicate detection is active.</summary>
        public bool IssueSimilarityEnabled { get; set; } = true;

        /// <summary>Minimum Jaccard similarity, from 0.0 to 1.0, for a finding to be marked duplicate.</summary>
        public double IssueSimilarityThreshold { get; set; } = Consts.DefaultIssueSimilarityThreshold;

        /// <summary>Minimum token length considered when tokenising finding text.</summary>
        public int IssueSimilarityMinimumTokenLength { get; set; } = Consts.DefaultIssueSimilarityMinimumTokenLength;

        /// <summary>Default page size used when browsing issues.</summary>
        public int IssueDefaultPageSize { get; set; } = Consts.IssueDefaultPageSize;

        /// <summary>Comma-separated page sizes offered when browsing issues.</summary>
        public string IssuePageSizeOptions { get; set; } = IssuePageSizeOptionsFormatter.Format(new IssueViewingOptions().PageSizeOptions);
    }
}
