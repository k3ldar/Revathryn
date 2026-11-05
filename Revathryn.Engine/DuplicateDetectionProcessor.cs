using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Executes queued duplicate-detection items by comparing each finding against its repository
    /// peers and marking the closest match above the configured threshold.
    /// </summary>
    public sealed class DuplicateDetectionProcessor : IDuplicateDetectionProcessor
    {
        private readonly IDuplicateDetectionQueue _queue;
        private readonly IFindingRepository _findingRepository;
        private readonly IIssueSimilarityService _similarityService;
        private readonly IIssueSettingsProvider _issueSettingsProvider;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly ResilienceOptions _resilienceOptions;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new processor.</summary>
        public DuplicateDetectionProcessor(
            IDuplicateDetectionQueue queue,
            IFindingRepository findingRepository,
            IIssueSimilarityService similarityService,
            IIssueSettingsProvider issueSettingsProvider,
            IRetryPolicyProvider retryPolicyProvider,
            ResilienceOptions resilienceOptions,
            IOperationalDiagnostics diagnostics)
        {
            _queue = queue;
            _findingRepository = findingRepository;
            _similarityService = similarityService;
            _issueSettingsProvider = issueSettingsProvider;
            _retryPolicyProvider = retryPolicyProvider;
            _resilienceOptions = resilienceOptions;
            _diagnostics = diagnostics;
        }

        /// <summary>Claims and executes the next due item. Returns false when nothing was due.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            if (!_issueSettingsProvider.IssueSimilarity.Enabled)
            {
                return false;
            }

            DuplicateDetectionQueueItem claimed = await _queue
                .ClaimNextAsync(DateTime.UtcNow, _resilienceOptions.QueueLeaseDuration, cancellationToken)
                .ConfigureAwait(false);

            if (claimed is null)
            {
                return false;
            }

            try
            {
                await ExecuteAsync(claimed, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _queue
                    .ReleaseForRetryAsync(claimed.Id, DateTime.UtcNow, string.Empty, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                await HandleUnexpectedAsync(claimed, exception).ConfigureAwait(false);
            }

            return true;
        }

        private async Task ExecuteAsync(DuplicateDetectionQueueItem claimed, CancellationToken cancellationToken)
        {
            int maxAttempts = _resilienceOptions.GetRetryPolicy(RetryOperationType.Database).MaxAttempts;

            if (claimed.AttemptCount > maxAttempts)
            {
                await FailAsync(claimed, Strings.DuplicateDetectionRetryLimitExceeded).ConfigureAwait(false);
                return;
            }

            Finding finding = await _findingRepository.GetByIdAsync(claimed.FindingId, cancellationToken).ConfigureAwait(false);

            if (finding is null)
            {
                await FailAsync(claimed, Strings.DuplicateDetectionFindingNotFound).ConfigureAwait(false);
                return;
            }

            IReadOnlyList<Finding> candidates = await _findingRepository
                .GetSimilarityCandidatesAsync(claimed.RepositoryId, cancellationToken)
                .ConfigureAwait(false);

            Finding bestMatch = null;
            double bestScore = Consts.IssueSimilarityMinimumScore;

            foreach (Finding candidate in candidates)
            {
                if (candidate.Id == finding.Id)
                {
                    continue;
                }

                double score = _similarityService.CalculateSimilarity(finding, candidate);

                if (bestMatch is null || score > bestScore || (score == bestScore && IsEarlier(candidate, bestMatch)))
                {
                    bestMatch = candidate;
                    bestScore = score;
                }
            }

            if (bestMatch is not null && bestScore >= _issueSettingsProvider.IssueSimilarity.Threshold)
            {
                long originalId = bestMatch.DuplicateOfFindingId ?? bestMatch.Id;

                await _findingRepository
                    .MarkDuplicateAsync(finding.Id, originalId, bestScore, DateTime.UtcNow, CancellationToken.None)
                    .ConfigureAwait(false);

                _diagnostics.Log(
                    OperationalLogLevel.Information,
                    Consts.DuplicateDetectionProcessorComponent,
                    Consts.ProcessDuplicateDetectionOperation,
                    string.Format(Strings.DuplicateDetectionMatchedLog, claimed.Id, finding.Id, originalId, bestScore));
            }
            else
            {
                _diagnostics.Log(
                    OperationalLogLevel.Information,
                    Consts.DuplicateDetectionProcessorComponent,
                    Consts.ProcessDuplicateDetectionOperation,
                    string.Format(Strings.DuplicateDetectionNoMatchLog, claimed.Id, finding.Id));
            }

            await _queue.CompleteAsync(claimed.Id, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task HandleUnexpectedAsync(DuplicateDetectionQueueItem claimed, Exception exception)
        {
            IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.Database);
            RetryDecision decision = policy.Evaluate(claimed.AttemptCount, exception);
            string message = string.IsNullOrWhiteSpace(exception.Message) ? Strings.UnknownReviewError : exception.Message;

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.DuplicateDetectionProcessorComponent,
                Consts.ProcessDuplicateDetectionOperation,
                message,
                exception,
                retryAttempt: claimed.AttemptCount);

            if (decision.ShouldRetry)
            {
                await _queue
                    .ReleaseForRetryAsync(claimed.Id, DateTime.UtcNow.Add(decision.Delay), message, CancellationToken.None)
                    .ConfigureAwait(false);
                return;
            }

            await FailAsync(claimed, message).ConfigureAwait(false);
        }

        private async Task FailAsync(DuplicateDetectionQueueItem claimed, string message)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? Strings.UnknownReviewError : message;

            await _queue.FailAsync(claimed.Id, safeMessage, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.DuplicateDetectionProcessorComponent,
                Consts.ProcessDuplicateDetectionOperation,
                string.Format(Strings.DuplicateDetectionFailedLog, claimed.Id, claimed.FindingId, safeMessage),
                retryAttempt: claimed.AttemptCount);
        }

        private static bool IsEarlier(Finding candidate, Finding current)
        {
            return candidate.CreatedDate < current.CreatedDate
                || (candidate.CreatedDate == current.CreatedDate && candidate.Id < current.Id);
        }
    }
}
