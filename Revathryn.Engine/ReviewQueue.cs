using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Engine
{
    /// <summary>Default priority-ordered review queue backed by the persistence layer.</summary>
    public sealed class ReviewQueue : IReviewQueue
    {
        private readonly IQueueRepository _queueRepository;

        /// <summary>Initialises a new queue.</summary>
        public ReviewQueue(IQueueRepository queueRepository)
        {
            _queueRepository = queueRepository;
        }

        /// <summary>Enqueues a review operation, ignoring duplicates already pending.</summary>
        public Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
        {
            return _queueRepository.EnqueueAsync(item, cancellationToken);
        }

        /// <summary>Claims the highest priority eligible pending item, or null when empty.</summary>
        public Task<QueueItem> ClaimNextAsync(DateTime leasedUntilUtc, CancellationToken cancellationToken = default)
        {
            return _queueRepository.ClaimNextAsync(leasedUntilUtc, cancellationToken);
        }

        /// <summary>Returns a claimed item to the pending state for a later retry.</summary>
        public Task ReleaseForRetryAsync(long id, int priority, DateTime nextAttemptUtc, string lastError, CancellationToken cancellationToken = default)
        {
            return _queueRepository.ReleaseForRetryAsync(id, priority, nextAttemptUtc, lastError, cancellationToken);
        }

        /// <summary>Removes the item because it completed successfully.</summary>
        public Task MarkCompletedAsync(long id, CancellationToken cancellationToken = default)
        {
            return _queueRepository.MarkCompletedAsync(id, cancellationToken);
        }

        /// <summary>Returns pending items ordered by priority.</summary>
        public Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            return _queueRepository.GetPendingAsync(cancellationToken);
        }

        /// <summary>Removes the queue item with the given id.</summary>
        public Task RemoveAsync(long id, CancellationToken cancellationToken = default)
        {
            return _queueRepository.RemoveAsync(id, cancellationToken);
        }

        /// <summary>Returns aggregated queue reliability metrics.</summary>
        public Task<QueueMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
        {
            return _queueRepository.GetMetricsAsync(cancellationToken);
        }
    }
}
