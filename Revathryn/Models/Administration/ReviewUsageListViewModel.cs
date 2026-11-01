using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>View model for the review usage history page.</summary>
    public sealed class ReviewUsageListViewModel
    {
        /// <summary>Aggregated usage metrics across all time.</summary>
        public ReviewUsageSummary Summary { get; set; } = new();

        /// <summary>Individual usage records, most recent first.</summary>
        public IReadOnlyList<ReviewUsage> UsageRecords { get; set; } = [];
    }
}
