using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for the transient review queue.</summary>
    public interface IQueueRepository
    {
        /// <summary>Inserts a queue item if an equivalent pending item does not already exist.</summary>
        /// <returns>True when the item was inserted; false when a duplicate was ignored.</returns>
        Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default);

        /// <summary>Removes and returns the highest priority pending item, or null when empty.</summary>
        Task<QueueItem> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns pending items ordered by priority descending, then creation ascending.</summary>
        Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns pending items for a repository, ordered as in <see cref="GetPendingAsync"/>.</summary>
        Task<IReadOnlyList<QueueItem>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Removes the queue item with the given id.</summary>
        Task RemoveAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Removes all pending items for a file.</summary>
        Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Returns the number of pending items.</summary>
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }
}
