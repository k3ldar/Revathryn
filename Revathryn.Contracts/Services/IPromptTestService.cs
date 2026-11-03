using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Requests and queries isolated prompt test runs. A test replays the original detection scenario with a
    /// proposed prompt and stores results independently of production issue detection.
    /// </summary>
    public interface IPromptTestService
    {
        /// <summary>
        /// Queues an isolated test run for a prompt version against the originating issue's context.
        /// When one is already pending or running for the same version and issue, it is returned unchanged.
        /// </summary>
        Task<PromptTestRunStartResult> RequestAsync(long promptVersionId, long? originalIssueId, CancellationToken cancellationToken = default);

        /// <summary>Queues a new test run that retries a failed one. The failed record is left intact.</summary>
        Task<PromptTestRunStartResult> RetryAsync(long testRunId, CancellationToken cancellationToken = default);

        /// <summary>Returns a test run by id, or null when not found.</summary>
        Task<PromptTestRun> GetByIdAsync(long testRunId, CancellationToken cancellationToken = default);

        /// <summary>Returns all test runs for a prompt version, newest first.</summary>
        Task<IReadOnlyList<PromptTestRun>> GetByPromptVersionAsync(long promptVersionId, CancellationToken cancellationToken = default);
    }
}
