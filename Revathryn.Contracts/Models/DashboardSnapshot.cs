using Revathryn.Contracts.Reliability;

namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregated data required by the operational dashboard.</summary>
    public sealed class DashboardSnapshot
    {
        /// <summary>Per-repository dashboard state.</summary>
        public IReadOnlyList<RepositoryDashboardItem> Repositories { get; set; } = [];

        /// <summary>Number of configured repositories.</summary>
        public int ConfiguredRepositoryCount { get; set; }

        /// <summary>Number of repositories that have been successfully scanned at least once.</summary>
        public int DiscoveredRepositoryCount { get; set; }

        /// <summary>Most recent successful scan across repositories, or null when none.</summary>
        public DateTime? LastRepositoryScanUtc { get; set; }

        /// <summary>Number of analyses currently queued.</summary>
        public int QueuedAnalysisCount { get; set; }

        /// <summary>Reliability metrics for the review queue.</summary>
        public QueueMetrics QueueMetrics { get; set; } = new();

        /// <summary>Number of analyses that terminally failed and were recorded as operational failure findings.</summary>
        public int FailedAnalysisCount { get; set; }

        /// <summary>Outstanding issue summary.</summary>
        public IssueSummary IssueSummary { get; set; } = new();

        /// <summary>Readiness health report.</summary>
        public HealthReport HealthReport { get; set; } = new();

        /// <summary>UTC timestamp when the application started.</summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>Elapsed time since the application started.</summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>Current process resource metrics.</summary>
        public SystemResourceMetrics ResourceMetrics { get; set; } = new();

        /// <summary>Most recent scan records across repositories.</summary>
        public IReadOnlyList<RecentScanItem> RecentScans { get; set; } = [];

        /// <summary>Aggregated AI review usage metrics.</summary>
        public ReviewUsageSummary ReviewUsage { get; set; } = new();
    }
}
