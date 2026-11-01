namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregated reliability metrics for the review queue.</summary>
    public sealed class QueueMetrics
    {
        /// <summary>Number of items waiting to be processed.</summary>
        public int PendingCount { get; set; }

        /// <summary>Number of pending items awaiting at least one retry.</summary>
        public int RetryCount { get; set; }

        /// <summary>UTC timestamp of the oldest pending item, or null when the queue is empty.</summary>
        public DateTime? OldestPendingUtc { get; set; }
    }
}
