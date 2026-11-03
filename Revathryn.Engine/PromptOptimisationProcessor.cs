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
    /// Executes queued prompt optimisations using the independent review engine. Stage 1 analyses the disagreement;
    /// stage 2 generates proposals only when the analysis recommends a change. It never activates a prompt and never
    /// modifies the originating issue, validation or detection record.
    /// </summary>
    public sealed class PromptOptimisationProcessor : IPromptOptimisationProcessor
    {
        private readonly IPromptOptimisationRepository _optimisationRepository;
        private readonly IReviewEngineSettingsRepository _settingsRepository;
        private readonly IPromptOptimisationContextBuilder _contextBuilder;
        private readonly IPromptAnalysisEngine _analysisEngine;
        private readonly IPromptProposalEngine _proposalEngine;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly ResilienceOptions _resilienceOptions;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new processor.</summary>
        public PromptOptimisationProcessor(
            IPromptOptimisationRepository optimisationRepository,
            IReviewEngineSettingsRepository settingsRepository,
            IPromptOptimisationContextBuilder contextBuilder,
            IPromptAnalysisEngine analysisEngine,
            IPromptProposalEngine proposalEngine,
            IRetryPolicyProvider retryPolicyProvider,
            ResilienceOptions resilienceOptions,
            IOperationalDiagnostics diagnostics)
        {
            _optimisationRepository = optimisationRepository;
            _settingsRepository = settingsRepository;
            _contextBuilder = contextBuilder;
            _analysisEngine = analysisEngine;
            _proposalEngine = proposalEngine;
            _retryPolicyProvider = retryPolicyProvider;
            _resilienceOptions = resilienceOptions;
            _diagnostics = diagnostics;
        }

        /// <summary>Claims and executes the next due optimisation. Returns false when nothing was due.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            PromptOptimisation claimed = await _optimisationRepository
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
                await _optimisationRepository
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

        private async Task ExecuteAsync(PromptOptimisation claimed, CancellationToken cancellationToken)
        {
            int maxAttempts = _resilienceOptions.GetRetryPolicy(RetryOperationType.ExternalService).MaxAttempts;

            if (claimed.AttemptCount > maxAttempts)
            {
                await FailAsync(claimed, Strings.PromptOptimisationRetryLimitExceeded, FailureCategory.Transient, string.Empty, null).ConfigureAwait(false);
                return;
            }

            ReviewEngineSettings settings = await _settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);

            if (settings is null || !settings.Enabled)
            {
                await FailAsync(claimed, Strings.ReviewEngineUnavailableAtExecution, FailureCategory.Configuration, string.Empty, null).ConfigureAwait(false);
                return;
            }

            PromptOptimisationContext context = await _contextBuilder.BuildAsync(claimed, cancellationToken).ConfigureAwait(false);

            claimed.Provider = settings.Provider;
            claimed.Model = settings.Model;
            claimed.EngineConfigSnapshotJson = IssueValidationJson.SerializeEngineSnapshot(settings);

            await _optimisationRepository.SaveExecutionSnapshotAsync(claimed, cancellationToken).ConfigureAwait(false);

            PromptAnalysisEngineResult analysisResult = await _analysisEngine.AnalyseAsync(
                new PromptAnalysisRequest
                {
                    Settings = settings,
                    SystemPrompt = context.AnalysisSystemPrompt,
                    UserPrompt = context.AnalysisUserPrompt
                },
                cancellationToken).ConfigureAwait(false);

            if (!analysisResult.Success)
            {
                await HandleEngineFailureAsync(claimed, analysisResult, maxAttempts).ConfigureAwait(false);
                return;
            }

            PromptAnalysisResult analysis = analysisResult.Analysis;

            claimed.AnalysisJson = PromptOptimisationJson.SerializeAnalysis(analysis);
            claimed.RawResponse = analysisResult.RawResponse;
            claimed.PromptTokens = analysisResult.Usage?.PromptTokens ?? 0;
            claimed.CompletionTokens = analysisResult.Usage?.CompletionTokens ?? 0;
            claimed.TotalTokens = analysisResult.Usage?.TotalTokens ?? 0;

            await _optimisationRepository.RecordAnalysisAsync(claimed, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptOptimisationProcessorComponent,
                Consts.AnalysePromptOperation,
                string.Format(Strings.PromptOptimisationAnalysedLog, claimed.Id));

            bool systemRequired = analysis.SystemPromptRecommendation?.Required is true;
            bool reviewRequired = analysis.ReviewPromptRecommendation?.Required is true;

            if (!systemRequired && !reviewRequired)
            {
                await CompleteWithoutProposalsAsync(claimed).ConfigureAwait(false);
                return;
            }

            string proposalUserPrompt = await _contextBuilder
                .BuildProposalUserPromptAsync(claimed, analysis, cancellationToken)
                .ConfigureAwait(false);

            PromptProposalEngineResult proposalResult = await _proposalEngine.GenerateAsync(
                new PromptProposalRequest
                {
                    Settings = settings,
                    SystemPrompt = context.ProposalSystemPrompt,
                    UserPrompt = proposalUserPrompt
                },
                cancellationToken).ConfigureAwait(false);

            if (!proposalResult.Success)
            {
                await HandleEngineFailureAsync(claimed, proposalResult, maxAttempts).ConfigureAwait(false);
                return;
            }

            PromptRecommendation systemRecommendation = proposalResult.SystemRecommendation;
            PromptRecommendation reviewRecommendation = proposalResult.ReviewRecommendation;

            AssignSourceAnalysisId(systemRecommendation, claimed.Id);
            AssignSourceAnalysisId(reviewRecommendation, claimed.Id);

            claimed.SystemPromptRecommendationJson = PromptOptimisationJson.SerializeRecommendation(systemRecommendation);
            claimed.ReviewPromptRecommendationJson = PromptOptimisationJson.SerializeRecommendation(reviewRecommendation);
            claimed.RawResponse = proposalResult.RawResponse;
            claimed.PromptTokens = proposalResult.Usage?.PromptTokens ?? 0;
            claimed.CompletionTokens = proposalResult.Usage?.CompletionTokens ?? 0;
            claimed.TotalTokens = proposalResult.Usage?.TotalTokens ?? 0;
            claimed.CompletedUtc = DateTime.UtcNow;

            await _optimisationRepository.RecordProposalsAsync(claimed, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptOptimisationProcessorComponent,
                Consts.GeneratePromptProposalOperation,
                string.Format(Strings.PromptOptimisationCompletedLog, claimed.Id));
        }

        private async Task CompleteWithoutProposalsAsync(PromptOptimisation claimed)
        {
            claimed.SystemPromptRecommendationJson = string.Empty;
            claimed.ReviewPromptRecommendationJson = string.Empty;
            claimed.RawResponse = string.Empty;
            claimed.PromptTokens = 0;
            claimed.CompletionTokens = 0;
            claimed.TotalTokens = 0;
            claimed.CompletedUtc = DateTime.UtcNow;

            await _optimisationRepository.RecordProposalsAsync(claimed, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptOptimisationProcessorComponent,
                Consts.GeneratePromptProposalOperation,
                string.Format(Strings.PromptOptimisationNoChangesLog, claimed.Id));
        }

        private async Task HandleEngineFailureAsync(PromptOptimisation claimed, PromptAnalysisEngineResult result, int maxAttempts)
        {
            bool transient = result.FailureKind is AICompletionFailureKind.Timeout
                or AICompletionFailureKind.RateLimited
                or AICompletionFailureKind.Transient;

            if (transient && claimed.AttemptCount < maxAttempts)
            {
                IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
                TimeSpan delay = result.RetryAfter ?? policy.GetRetryDelay(claimed.AttemptCount);

                await _optimisationRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(delay),
                    result.ErrorMessage,
                    result.RawResponse,
                    result.Usage,
                    CancellationToken.None).ConfigureAwait(false);

                _diagnostics.Log(
                    OperationalLogLevel.Warning,
                    Consts.PromptOptimisationProcessorComponent,
                    Consts.ProcessOptimisationOperation,
                    result.ErrorMessage,
                    retryAttempt: claimed.AttemptCount);
                return;
            }

            await FailAsync(claimed, result.ErrorMessage, ClassifyFailure(result.FailureKind, result.Malformed), result.RawResponse, result.Usage).ConfigureAwait(false);
        }

        private async Task HandleEngineFailureAsync(PromptOptimisation claimed, PromptProposalEngineResult result, int maxAttempts)
        {
            bool transient = result.FailureKind is AICompletionFailureKind.Timeout
                or AICompletionFailureKind.RateLimited
                or AICompletionFailureKind.Transient;

            if (transient && claimed.AttemptCount < maxAttempts)
            {
                IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
                TimeSpan delay = result.RetryAfter ?? policy.GetRetryDelay(claimed.AttemptCount);

                await _optimisationRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(delay),
                    result.ErrorMessage,
                    result.RawResponse,
                    result.Usage,
                    CancellationToken.None).ConfigureAwait(false);

                _diagnostics.Log(
                    OperationalLogLevel.Warning,
                    Consts.PromptOptimisationProcessorComponent,
                    Consts.ProcessOptimisationOperation,
                    result.ErrorMessage,
                    retryAttempt: claimed.AttemptCount);
                return;
            }

            await FailAsync(claimed, result.ErrorMessage, ClassifyFailure(result.FailureKind, result.Malformed), result.RawResponse, result.Usage).ConfigureAwait(false);
        }

        private async Task HandleUnexpectedAsync(PromptOptimisation claimed, Exception exception)
        {
            IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
            RetryDecision decision = policy.Evaluate(claimed.AttemptCount, exception);
            string message = string.IsNullOrWhiteSpace(exception.Message) ? Strings.UnknownReviewError : exception.Message;

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.PromptOptimisationProcessorComponent,
                Consts.ProcessOptimisationOperation,
                message,
                exception,
                retryAttempt: claimed.AttemptCount);

            if (decision.ShouldRetry)
            {
                await _optimisationRepository.ReleaseForRetryAsync(
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

        private async Task FailAsync(PromptOptimisation claimed, string message, FailureCategory category, string rawResponse, TokenUsage usage)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? Strings.UnknownReviewError : message;

            await _optimisationRepository.FailAsync(claimed.Id, safeMessage, category, rawResponse, usage, DateTime.UtcNow, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.PromptOptimisationProcessorComponent,
                Consts.ProcessOptimisationOperation,
                string.Format(Strings.PromptOptimisationFailedLog, claimed.Id, safeMessage),
                retryAttempt: claimed.AttemptCount);
        }

        private static void AssignSourceAnalysisId(PromptRecommendation recommendation, long analysisId)
        {
            if (recommendation is null)
            {
                return;
            }

            recommendation.SourceAnalysisId = analysisId;
        }

        private static FailureCategory ClassifyFailure(AICompletionFailureKind failureKind, bool malformed)
        {
            if (malformed)
            {
                return FailureCategory.DataIntegrity;
            }

            return failureKind switch
            {
                AICompletionFailureKind.Authentication or AICompletionFailureKind.Configuration => FailureCategory.Configuration,
                AICompletionFailureKind.Timeout or AICompletionFailureKind.RateLimited or AICompletionFailureKind.Transient => FailureCategory.Transient,
                _ => FailureCategory.Permanent
            };
        }
    }
}
