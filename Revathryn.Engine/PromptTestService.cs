using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Requests and queries isolated prompt test runs. A test replays the original detection scenario with a
    /// proposed prompt and stores results independently of production issue detection.
    /// </summary>
    public sealed class PromptTestService : IPromptTestService
    {
        private readonly IPromptVersionRepository _versionRepository;
        private readonly IFindingRepository _findingRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IAIEngineRepository _engineRepository;
        private readonly IPromptTestRunRepository _testRunRepository;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public PromptTestService(
            IPromptVersionRepository versionRepository,
            IFindingRepository findingRepository,
            IReviewContextRepository reviewContextRepository,
            IAIEngineRepository engineRepository,
            IPromptTestRunRepository testRunRepository,
            IOperationalDiagnostics diagnostics)
        {
            _versionRepository = versionRepository;
            _findingRepository = findingRepository;
            _reviewContextRepository = reviewContextRepository;
            _engineRepository = engineRepository;
            _testRunRepository = testRunRepository;
            _diagnostics = diagnostics;
        }

        /// <summary>Queues an isolated test run for a prompt version against the originating issue's context.</summary>
        public Task<PromptTestRunStartResult> RequestAsync(long promptVersionId, long? originalIssueId, CancellationToken cancellationToken = default)
        {
            return QueueAsync(promptVersionId, originalIssueId, cancellationToken);
        }

        /// <summary>Queues a new test run that retries a failed one. The failed record is left intact.</summary>
        public async Task<PromptTestRunStartResult> RetryAsync(long testRunId, CancellationToken cancellationToken = default)
        {
            PromptTestRun failed = await _testRunRepository.GetByIdAsync(testRunId, cancellationToken).ConfigureAwait(false);

            if (failed is null || failed.Status is not PromptTestRunStatus.Failed)
            {
                return Rejected(PromptTestRunRejection.NotRetryable, Strings.PromptTestRunNotRetryable);
            }

            return await QueueAsync(failed.PromptVersionId, failed.OriginalIssueId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns a test run by id, or null when not found.</summary>
        public Task<PromptTestRun> GetByIdAsync(long testRunId, CancellationToken cancellationToken = default)
        {
            return _testRunRepository.GetByIdAsync(testRunId, cancellationToken);
        }

        /// <summary>Returns all test runs for a prompt version, newest first.</summary>
        public Task<IReadOnlyList<PromptTestRun>> GetByPromptVersionAsync(long promptVersionId, CancellationToken cancellationToken = default)
        {
            return _testRunRepository.GetByPromptVersionAsync(promptVersionId, cancellationToken);
        }

        private async Task<PromptTestRunStartResult> QueueAsync(long promptVersionId, long? originalIssueId, CancellationToken cancellationToken)
        {
            PromptVersion version = await _versionRepository.GetByIdAsync(promptVersionId, cancellationToken).ConfigureAwait(false);

            if (version is null)
            {
                return Rejected(PromptTestRunRejection.PromptVersionMissing, Strings.PromptTestRunPromptVersionMissing);
            }

            AIReviewContext original = null;

            if (originalIssueId.HasValue)
            {
                Finding finding = await _findingRepository.GetByIdAsync(originalIssueId.Value, cancellationToken).ConfigureAwait(false);

                if (finding?.ReviewContextId.HasValue is true)
                {
                    original = await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken).ConfigureAwait(false);
                }

                if (original is null)
                {
                    return Rejected(PromptTestRunRejection.ContextMissing, Strings.PromptTestRunContextMissing);
                }
            }

            IReadOnlyList<AIEngine> enabledEngines = await _engineRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);

            if (enabledEngines.Count == 0)
            {
                return Rejected(PromptTestRunRejection.DetectionEngineUnavailable, Strings.PromptTestRunDetectionUnavailable);
            }

            PromptTestRun active = await _testRunRepository.GetActiveByVersionAndIssueAsync(promptVersionId, originalIssueId, cancellationToken).ConfigureAwait(false);

            if (active is not null)
            {
                return AlreadyActive(active);
            }

            PromptTestRun testRun = new()
            {
                PromptVersionId = version.Id,
                ReviewTypeId = original?.ReviewTypeId,
                ReviewContextId = original?.Id,
                OriginalIssueId = originalIssueId,
                PromptText = version.PromptText,
                Status = PromptTestRunStatus.Pending,
                CreatedUtc = DateTime.UtcNow
            };

            long id = await _testRunRepository.TryAddAsync(testRun, cancellationToken).ConfigureAwait(false);

            if (id == 0)
            {
                PromptTestRun raced = await _testRunRepository.GetActiveByVersionAndIssueAsync(promptVersionId, originalIssueId, cancellationToken).ConfigureAwait(false);
                return raced is null
                    ? Rejected(PromptTestRunRejection.DetectionEngineUnavailable, Strings.PromptTestRunCouldNotBeQueued)
                    : AlreadyActive(raced);
            }

            testRun.Id = id;

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptTestRunServiceComponent,
                Consts.QueueTestRunOperation,
                string.Format(Strings.PromptTestRunQueuedLog, id, promptVersionId));

            return new PromptTestRunStartResult
            {
                TestRun = testRun,
                Created = true,
                Message = Strings.PromptTestRunQueued
            };
        }

        private static PromptTestRunStartResult AlreadyActive(PromptTestRun active)
        {
            return new PromptTestRunStartResult
            {
                TestRun = active,
                AlreadyActive = true,
                Message = Strings.PromptTestRunAlreadyInProgress
            };
        }

        private static PromptTestRunStartResult Rejected(PromptTestRunRejection rejection, string message)
        {
            return new PromptTestRunStartResult
            {
                Rejection = rejection,
                Message = message
            };
        }
    }
}
