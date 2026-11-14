namespace Revathryn.Models.Api
{
    /// <summary>Review coverage state for a single review type against a file.</summary>
    public sealed class FileReviewStatus
    {
        /// <summary>Review type identifier.</summary>
        public long ReviewTypeId { get; init; }

        /// <summary>Display name of the review type.</summary>
        public string ReviewTypeName { get; init; } = string.Empty;

        /// <summary>Whether the review is Completed or Outstanding for the file.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>UTC timestamp of the last completed analysis, when completed.</summary>
        public DateTime? LastAnalysedUtc { get; init; }

        /// <summary>Version of the analysis engine that produced the result.</summary>
        public string AnalysisVersion { get; init; } = string.Empty;

        /// <summary>Version of the review definition the analysis completed against.</summary>
        public string ReviewDefinitionVersion { get; init; } = string.Empty;

        /// <summary>Git branch the analysis was completed against.</summary>
        public string Branch { get; init; } = string.Empty;
    }
}
