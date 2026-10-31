namespace Revathryn.Contracts.Github
{
    /// <summary>
    /// In-memory queue for GitHub webhook events with JSON-file persistence.
    /// Registered as a singleton so the queue survives across requests.
    /// </summary>
    public interface IWebhookQueueService
    {
        /// <summary>
        /// Enqueues a new event and persists the queue to disk.
        /// </summary>
        Task EnqueueAsync(string repositoryName, string eventType, string rawBody, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a snapshot of all entries currently held in memory.
        /// </summary>
        IReadOnlyList<WebhookQueueEntry> GetAll();

        /// <summary>
        /// Returns only entries that have not yet been processed.
        /// </summary>
        IReadOnlyList<WebhookQueueEntry> GetPending();

        /// <summary>
        /// Marks an entry as processed by its <paramref name="id"/> and persists the change to disk.
        /// Returns <c>false</c> if no entry with that id exists.
        /// </summary>
        Task<bool> MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all entries that have already been processed and persists the result to disk.
        /// Returns the number of entries removed.
        /// </summary>
        Task<int> PurgeProcessedAsync(CancellationToken cancellationToken = default);
    }
}
