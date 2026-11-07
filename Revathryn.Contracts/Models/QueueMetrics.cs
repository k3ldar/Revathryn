namespace Revathryn.Contracts.Models
{
    /// <summary>Aggregated reliability metrics for the review queue.</summary>
    public sealed class QueueMetrics
    {
        /// <summary>Number of items waiting to be processed.</summary>
        public int PendingCount { get; set; }

        /// <summary>Number of pending items awaiting at least one retry.</summary>
        public int RetryCount { get; set; }

        /// <summary>Elapsed time since the oldest pending item was queued, or null when the queue is empty.</summary>
        public TimeSpan? OldestPendingAge { get; set; }
    }
}
