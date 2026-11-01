namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregated AI review usage metrics for the dashboard and administration pages.</summary>
    public sealed class ReviewUsageSummary
    {
        /// <summary>Total number of completed reviews across all time.</summary>
        public int TotalReviewCount { get; set; }

        /// <summary>Total prompt tokens across all time.</summary>
        public int TotalPromptTokens { get; set; }

        /// <summary>Total completion tokens across all time.</summary>
        public int TotalCompletionTokens { get; set; }

        /// <summary>Total tokens across all time.</summary>
        public int TotalTokens { get; set; }

        /// <summary>Total cached prompt tokens across all time.</summary>
        public int TotalCachedTokens { get; set; }

        /// <summary>Percentage of prompt tokens served from cache across all time.</summary>
        public double TotalCacheHitPercent { get; set; }

        /// <summary>Number of completed reviews for the current UTC day.</summary>
        public int TodayReviewCount { get; set; }

        /// <summary>Prompt tokens consumed during the current UTC day.</summary>
        public int TodayPromptTokens { get; set; }

        /// <summary>Completion tokens generated during the current UTC day.</summary>
        public int TodayCompletionTokens { get; set; }

        /// <summary>Total tokens consumed during the current UTC day.</summary>
        public int TodayTotalTokens { get; set; }

        /// <summary>Cached prompt tokens during the current UTC day.</summary>
        public int TodayCachedTokens { get; set; }

        /// <summary>Percentage of prompt tokens served from cache during the current UTC day.</summary>
        public double TodayCacheHitPercent { get; set; }
    }
}
