namespace Revathryn.Contracts.Models
{
    /// <summary>Filters for basic prompt performance statistics.</summary>
    public sealed class PromptStatisticsFilter
    {
        /// <summary>Optional review type filter. Null returns all review-specific prompts.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Optional prompt version filter.</summary>
        public long? PromptVersionId { get; set; }

        /// <summary>Optional detection model filter.</summary>
        public string DetectionModel { get; set; }

        /// <summary>Inclusive start of the date range. Null means no lower bound.</summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>Inclusive end of the date range. Null means no upper bound.</summary>
        public DateTime? ToUtc { get; set; }
    }
}
