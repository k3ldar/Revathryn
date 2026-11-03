using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for isolated prompt test runs. Results are stored independently of production issues.</summary>
    public interface IPromptTestRunRepository
    {
        /// <summary>
        /// Inserts a pending test run. Returns 0 without inserting when the same version and original issue
        /// already have a pending or running test run.
        /// </summary>
        Task<long> TryAddAsync(PromptTestRun testRun, CancellationToken cancellationToken = default);

        /// <summary>Returns a test run by id, or null when not found.</summary>
        Task<PromptTestRun> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all test runs for a prompt version, newest first.</summary>
        Task<IReadOnlyList<PromptTestRun>> GetByPromptVersionAsync(long promptVersionId, CancellationToken cancellationToken = default);

        /// <summary>Returns the pending or running test run for a prompt version and original issue, or null.</summary>
        Task<PromptTestRun> GetActiveByVersionAndIssueAsync(long promptVersionId, long? originalIssueId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims the next due test run under a lease, reclaiming any whose lease expired.
        /// Returns null when nothing is due.
        /// </summary>
        Task<PromptTestRun> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

        /// <summary>Records the context, prompt and engine configuration about to be tested.</summary>
        Task SaveExecutionSnapshotAsync(PromptTestRun testRun, CancellationToken cancellationToken = default);

        /// <summary>Persists the revised output and comparison and marks the test run completed.</summary>
        Task CompleteAsync(PromptTestRun testRun, CancellationToken cancellationToken = default);

        /// <summary>Marks the test run failed.</summary>
        Task FailAsync(long id, string errorMessage, FailureCategory category, string rawResponse, TokenUsage usage, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Returns the test run to the pending state for a later retry.</summary>
        Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string errorMessage, string rawResponse, TokenUsage usage, CancellationToken cancellationToken = default);
    }
}
