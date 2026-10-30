using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Orchestration
{
    /// <summary>The priority-ordered review queue.</summary>
    public interface IReviewQueue
    {
        /// <summary>Enqueues a review operation, ignoring duplicates already pending.</summary>
        Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default);

        /// <summary>Removes and returns the highest priority pending item, or null when empty.</summary>
        Task<QueueItem> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns pending items ordered by priority.</summary>
        Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default);

        /// <summary>Removes the queue item with the given id.</summary>
        Task RemoveAsync(long id, CancellationToken cancellationToken = default);
    }
}
