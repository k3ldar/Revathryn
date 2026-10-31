namespace Revathryn.Contracts.Github
{
    /// <summary>
    /// Represents a single GitHub webhook event held in the queue pending Phase 3 processing.
    /// </summary>
    public sealed class WebhookQueueEntry
    {
        /// <summary>Unique identifier for this queue entry.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        /// <summary>UTC timestamp when the event was received.</summary>
        public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>The repository name extracted from the route (e.g. "my-repo").</summary>
        public string RepositoryName { get; init; } = string.Empty;

        /// <summary>Value of the X-GitHub-Event header (e.g. "push", "pull_request").</summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>Raw JSON body exactly as received from GitHub.</summary>
        public string RawBody { get; init; } = string.Empty;

        /// <summary>Whether Phase 3 has finished processing this entry.</summary>
        public bool IsProcessed { get; set; }

        /// <summary>UTC timestamp set by Phase 3 when processing completes.</summary>
        public DateTimeOffset? ProcessedAt { get; set; }
    }
}
