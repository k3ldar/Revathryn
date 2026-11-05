namespace Revathryn.Models.Administration
{
    /// <summary>Form model for general platform settings.</summary>
    public sealed class GeneralSettingsViewModel
    {
        /// <summary>The configurable global master prompt prepended to every review prompt.</summary>
        public string MasterPrompt { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the settings were last modified.</summary>
        public DateTime LastModifiedDate { get; set; }

        /// <summary>Whether background duplicate detection is active.</summary>
        public bool IssueSimilarityEnabled { get; set; } = true;

        /// <summary>User-facing duplicate-detection threshold expressed as a whole percentage.</summary>
        public int IssueSimilarityThresholdPercent { get; set; }

        /// <summary>Minimum token length considered when tokenising finding text.</summary>
        public int IssueSimilarityMinimumTokenLength { get; set; }

        /// <summary>Default page size used when browsing issues.</summary>
        public int IssueDefaultPageSize { get; set; }

        /// <summary>Comma-separated page sizes offered when browsing issues.</summary>
        public string IssuePageSizeOptions { get; set; } = string.Empty;
    }
}
