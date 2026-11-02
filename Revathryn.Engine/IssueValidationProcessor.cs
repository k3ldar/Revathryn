using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Executes queued issue validations using the independent review engine.
    /// Provider, timeout, rate limit and malformed-output failures are retried or recorded as failed,
    /// never as an inconclusive verdict, and the validated finding is never modified.
    /// </summary>
    public sealed class IssueValidationProcessor : IIssueValidationProcessor
    {
        private readonly IIssueValidationRepository _validationRepository;
        private readonly IFindingRepository _findingRepository;
        private readonly IReviewEngineSettingsRepository _settingsRepository;
        private readonly IIssueValidationContextBuilder _contextBuilder;
        private readonly IIssueValidationEngine _validationEngine;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly ResilienceOptions _resilienceOptions;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new processor.</summary>
        public IssueValidationProcessor(
            IIssueValidationRepository validationRepository,
            IFindingRepository findingRepository,
            IReviewEngineSettingsRepository settingsRepository,
            IIssueValidationContextBuilder contextBuilder,
            IIssueValidationEngine validationEngine,
            IRetryPolicyProvider retryPolicyProvider,
            ResilienceOptions resilienceOptions,
            IOperationalDiagnostics diagnostics)
        {
            _validationRepository = validationRepository;
            _findingRepository = findingRepository;
            _settingsRepository = settingsRepository;
            _contextBuilder = contextBuilder;
            _validationEngine = validationEngine;
            _retryPolicyProvider = retryPolicyProvider;
            _resilienceOptions = resilienceOptions;
            _diagnostics = diagnostics;
        }

        /// <summary>Claims and executes the next due validation. Returns false when nothing was due.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            IssueValidation claimed = await _validationRepository
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
                await _validationRepository
                    .ReleaseForRetryAsync(claimed.Id, DateTime.UtcNow, string.Empty, string.Empty, null, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                await HandleUnexpectedAsync(claimed, exception).ConfigureAwait(false);
            }

            return true;
        }

        private async Task ExecuteAsync(IssueValidation claimed, CancellationToken cancellationToken)
        {
            int maxAttempts = _resilienceOptions.GetRetryPolicy(RetryOperationType.ExternalService).MaxAttempts;

            if (claimed.AttemptCount > maxAttempts)
            {
                await FailAsync(claimed, Strings.ValidationRetryLimitExceeded, FailureCategory.Transient, string.Empty, null).ConfigureAwait(false);
                return;
            }

            Finding finding = await _findingRepository.GetByIdAsync(claimed.FindingId, cancellationToken).ConfigureAwait(false);

            if (finding is null)
            {
                await FailAsync(claimed, Strings.ValidationIssueNotFound, FailureCategory.DataIntegrity, string.Empty, null).ConfigureAwait(false);
                return;
            }

            ReviewEngineSettings settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

            if (settings is null || !settings.Enabled)
            {
                await FailAsync(claimed, Strings.ReviewEngineUnavailableAtExecution, FailureCategory.Configuration, string.Empty, null).ConfigureAwait(false);
                return;
            }

            IssueValidationContext context = await _contextBuilder.BuildAsync(finding, cancellationToken).ConfigureAwait(false);

            claimed.Provider = settings.Provider;
            claimed.Model = settings.Model;
            claimed.EngineConfigSnapshotJson = IssueValidationJson.SerializeEngineSnapshot(settings);
            claimed.PromptVersion = context.PromptVersion;
            claimed.SystemPrompt = context.SystemPrompt;
            claimed.UserPrompt = context.UserPrompt;
            claimed.ContextReferenceJson = IssueValidationJson.Serialize(context.Reference);

            await _validationRepository.SaveExecutionSnapshotAsync(claimed, cancellationToken).ConfigureAwait(false);

            IssueValidationEngineResult result = await _validationEngine.ValidateAsync(
                new IssueValidationRequest
                {
                    Settings = settings,
                    SystemPrompt = context.SystemPrompt,
                    UserPrompt = context.UserPrompt,
                    SuppliedSources = context.SuppliedSources
                },
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                await CompleteAsync(claimed, result).ConfigureAwait(false);
                return;
            }

            await HandleEngineFailureAsync(claimed, result, maxAttempts).ConfigureAwait(false);
        }

        private async Task CompleteAsync(IssueValidation claimed, IssueValidationEngineResult result)
        {
            claimed.Verdict = result.Verdict;
            claimed.Confidence = result.Confidence;
            claimed.Summary = result.Summary;
            claimed.Reasoning = result.Reasoning;
            claimed.RecommendedSeverity = result.RecommendedSeverity;
            claimed.SeverityReasoning = result.SeverityReasoning;
            claimed.EvidenceJson = IssueValidationJson.Serialize(result.Evidence);
            claimed.SuggestionsJson = IssueValidationJson.Serialize(result.Suggestions);
            claimed.LimitationsJson = IssueValidationJson.Serialize(result.Limitations);
            claimed.RawResponse = result.RawResponse;
            claimed.PromptTokens = result.Usage?.PromptTokens ?? 0;
            claimed.CompletionTokens = result.Usage?.CompletionTokens ?? 0;
            claimed.TotalTokens = result.Usage?.TotalTokens ?? 0;
            claimed.CompletedUtc = DateTime.UtcNow;

            await _validationRepository.CompleteAsync(claimed, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.IssueValidationProcessorComponent,
                Consts.ProcessValidationOperation,
                string.Format(Strings.ValidationCompletedLog, claimed.Id, claimed.FindingId, result.Verdict));
        }

        private async Task HandleEngineFailureAsync(IssueValidation claimed, IssueValidationEngineResult result, int maxAttempts)
        {
            bool transient = result.FailureKind is AICompletionFailureKind.Timeout
                or AICompletionFailureKind.RateLimited
                or AICompletionFailureKind.Transient;

            if (transient && claimed.AttemptCount < maxAttempts)
            {
                IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
                TimeSpan delay = result.RetryAfter ?? policy.GetRetryDelay(claimed.AttemptCount);

                await _validationRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(delay),
                    result.ErrorMessage,
                    result.RawResponse,
                    result.Usage,
                    CancellationToken.None).ConfigureAwait(false);

                _diagnostics.Log(
                    OperationalLogLevel.Warning,
                    Consts.IssueValidationProcessorComponent,
                    Consts.ProcessValidationOperation,
                    result.ErrorMessage,
                    retryAttempt: claimed.AttemptCount);
                return;
            }

            await FailAsync(claimed, result.ErrorMessage, ClassifyFailure(result), result.RawResponse, result.Usage).ConfigureAwait(false);
        }

        private async Task HandleUnexpectedAsync(IssueValidation claimed, Exception exception)
        {
            IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
            RetryDecision decision = policy.Evaluate(claimed.AttemptCount, exception);
            string message = string.IsNullOrWhiteSpace(exception.Message) ? Strings.UnknownReviewError : exception.Message;

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.IssueValidationProcessorComponent,
                Consts.ProcessValidationOperation,
                message,
                exception,
                retryAttempt: claimed.AttemptCount);

            if (decision.ShouldRetry)
            {
                await _validationRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(decision.Delay),
                    message,
                    string.Empty,
                    null,
                    CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await FailAsync(claimed, message, decision.Category, string.Empty, null).ConfigureAwait(false);
        }

        private async Task FailAsync(IssueValidation claimed, string message, FailureCategory category, string rawResponse, TokenUsage usage)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? Strings.UnknownReviewError : message;

            await _validationRepository.FailAsync(claimed.Id, safeMessage, category, rawResponse, usage, DateTime.UtcNow, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.IssueValidationProcessorComponent,
                Consts.ProcessValidationOperation,
                string.Format(Strings.ValidationFailedLog, claimed.Id, claimed.FindingId, safeMessage),
                retryAttempt: claimed.AttemptCount);
        }

        private static FailureCategory ClassifyFailure(IssueValidationEngineResult result)
        {
            if (result.Malformed)
            {
                return FailureCategory.DataIntegrity;
            }

            return result.FailureKind switch
            {
                AICompletionFailureKind.Authentication or AICompletionFailureKind.Configuration => FailureCategory.Configuration,
                AICompletionFailureKind.Timeout or AICompletionFailureKind.RateLimited or AICompletionFailureKind.Transient => FailureCategory.Transient,
                _ => FailureCategory.Permanent
            };
        }
    }
}
