using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Orchestration
{
    /// <summary>The priority-ordered review queue.</summary>
    public interface IReviewQueue
    {
        /// <summary>Enqueues a review operation, ignoring duplicates already pending.</summary>
        Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default);

        /// <summary>Claims the highest priority eligible pending item, or null when empty.</summary>
        Task<QueueItem> ClaimNextAsync(DateTime leasedUntilUtc, CancellationToken cancellationToken = default);

        /// <summary>Returns a claimed item to the pending state for a later retry.</summary>
        Task ReleaseForRetryAsync(long id, int priority, DateTime nextAttemptUtc, string lastError, CancellationToken cancellationToken = default);

        /// <summary>Removes the item because it completed successfully.</summary>
        Task MarkCompletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns pending items ordered by priority.</summary>
        Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default);

        /// <summary>Removes the queue item with the given id.</summary>
        Task RemoveAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns aggregated queue reliability metrics.</summary>
        Task<QueueMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
    }
}
