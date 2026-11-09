using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Durable SQLite-backed queue for duplicate detection work items.</summary>
    public interface IDuplicateDetectionQueue
    {
        /// <summary>Enqueues a finding for duplicate evaluation and returns the new item id.</summary>
        Task<long> EnqueueAsync(long repositoryId, long findingId, CancellationToken cancellationToken = default);

        /// <summary>Claims the next due item under a lease, or null when nothing is due.</summary>
        Task<DuplicateDetectionQueueItem> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

        /// <summary>Marks the item completed when the caller still owns the current attempt, and reports whether it matched.</summary>
        Task<bool> CompleteAsync(long id, int attemptCount, CancellationToken cancellationToken = default);

        /// <summary>Marks the item permanently failed when the caller still owns the current attempt, and reports whether it matched.</summary>
        Task<bool> FailAsync(long id, int attemptCount, string errorMessage, CancellationToken cancellationToken = default);

        /// <summary>Returns the item to the pending state when the caller still owns the current attempt, and reports whether it matched.</summary>
        Task<bool> ReleaseForRetryAsync(long id, int attemptCount, DateTime nextAttemptUtc, string errorMessage, CancellationToken cancellationToken = default);
    }
}
