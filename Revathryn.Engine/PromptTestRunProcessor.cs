using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Executes queued isolated prompt test runs. It replays the original detection context through the detection
    /// engine with the tested prompt substituted, persists the revised output and a structured comparison, and
    /// never creates or modifies production issues.
    /// </summary>
    public sealed class PromptTestRunProcessor : IPromptTestRunProcessor
    {
        private readonly IPromptTestRunRepository _testRunRepository;
        private readonly IPromptVersionRepository _versionRepository;
        private readonly IPromptRepository _promptRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IFindingRepository _findingRepository;
        private readonly IAIEngineRepository _engineRepository;
        private readonly IAIReviewEngine _aiReviewEngine;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly ResilienceOptions _resilienceOptions;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new processor.</summary>
        public PromptTestRunProcessor(
            IPromptTestRunRepository testRunRepository,
            IPromptVersionRepository versionRepository,
            IPromptRepository promptRepository,
            IReviewContextRepository reviewContextRepository,
            IFindingRepository findingRepository,
            IAIEngineRepository engineRepository,
            IAIReviewEngine aiReviewEngine,
            IRetryPolicyProvider retryPolicyProvider,
            ResilienceOptions resilienceOptions,
            IOperationalDiagnostics diagnostics)
        {
            _testRunRepository = testRunRepository;
            _versionRepository = versionRepository;
            _promptRepository = promptRepository;
            _reviewContextRepository = reviewContextRepository;
            _findingRepository = findingRepository;
            _engineRepository = engineRepository;
            _aiReviewEngine = aiReviewEngine;
            _retryPolicyProvider = retryPolicyProvider;
            _resilienceOptions = resilienceOptions;
            _diagnostics = diagnostics;
        }

        /// <summary>Claims and executes the next due test run. Returns false when nothing was due.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            PromptTestRun claimed = await _testRunRepository
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
                await _testRunRepository
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

        private async Task ExecuteAsync(PromptTestRun claimed, CancellationToken cancellationToken)
        {
            int maxAttempts = _resilienceOptions.GetRetryPolicy(RetryOperationType.ExternalService).MaxAttempts;

            if (claimed.AttemptCount > maxAttempts)
            {
                await FailAsync(claimed, Strings.PromptTestRunRetryLimitExceeded, FailureCategory.Transient, null).ConfigureAwait(false);
                return;
            }

            PromptVersion version = await _versionRepository.GetByIdAsync(claimed.PromptVersionId, cancellationToken).ConfigureAwait(false);

            if (version is null)
            {
                await FailAsync(claimed, Strings.PromptTestRunPromptVersionMissing, FailureCategory.DataIntegrity, null).ConfigureAwait(false);
                return;
            }

            Prompt prompt = await _promptRepository.GetByIdAsync(version.PromptId, cancellationToken).ConfigureAwait(false);

            if (prompt is null)
            {
                await FailAsync(claimed, Strings.PromptTestRunPromptIdentityMissing, FailureCategory.DataIntegrity, null).ConfigureAwait(false);
                return;
            }

            AIReviewContext original = claimed.ReviewContextId.HasValue
                ? await _reviewContextRepository.GetByIdAsync(claimed.ReviewContextId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            if (original is null)
            {
                await FailAsync(claimed, Strings.PromptTestRunContextMissing, FailureCategory.DataIntegrity, null).ConfigureAwait(false);
                return;
            }

            if (prompt.PromptType is PromptType.Review && prompt.ReviewTypeId != original.ReviewTypeId)
            {
                await FailAsync(claimed, Strings.PromptTestRunReviewTypeMismatch, FailureCategory.DataIntegrity, null).ConfigureAwait(false);
                return;
            }

            AIEngine engine = await ResolveEngineAsync(cancellationToken).ConfigureAwait(false);

            if (engine is null)
            {
                await FailAsync(claimed, Strings.PromptTestRunDetectionUnavailable, FailureCategory.Configuration, null).ConfigureAwait(false);
                return;
            }

            Finding finding = claimed.OriginalIssueId.HasValue
                ? await _findingRepository.GetByIdAsync(claimed.OriginalIssueId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            string combinedPrompt = BuildTestPrompt(prompt, version, original);

            claimed.ReviewTypeId = original.ReviewTypeId;
            claimed.DetectionModel = original.EngineModel ?? string.Empty;
            claimed.ReviewContextId = original.Id;
            claimed.ContextSnapshotJson = PromptTestRunJson.SerializeContextSnapshot(
                original.Id,
                original.Sha256,
                original.FilePath,
                original.Language,
                claimed.OriginalIssueId,
                combinedPrompt);
            claimed.PromptText = version.PromptText ?? string.Empty;
            claimed.OriginalOutputJson = BuildOriginalOutputJson(finding, original);
            claimed.Provider = engine.Provider;
            claimed.Model = engine.Model;
            claimed.EngineConfigSnapshotJson = PromptTestRunJson.SerializeEngineSnapshot(engine);

            await _testRunRepository.SaveExecutionSnapshotAsync(claimed, cancellationToken).ConfigureAwait(false);

            ReviewRequest request = new()
            {
                RepositoryId = original.RepositoryId,
                RepoFileId = original.RepoFileId,
                ReviewTypeId = original.ReviewTypeId,
                Language = original.Language ?? string.Empty,
                FileContent = original.FileContent ?? string.Empty,
                Prompt = combinedPrompt,
                InitialContext = [.. original.ContextItems.Select(item => item.Content ?? string.Empty)]
            };

            ReviewResponse response = await _aiReviewEngine.ReviewAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                await HandleEngineFailureAsync(claimed, response, maxAttempts).ConfigureAwait(false);
                return;
            }

            if (response.Results is null)
            {
                await FailAsync(claimed, Strings.AIEngineResponseIncomplete, FailureCategory.DataIntegrity, response.Usage).ConfigureAwait(false);
                return;
            }

            PromptTestComparison comparison = BuildComparison(finding, original, engine, response);

            claimed.TestOutputJson = PromptTestRunJson.SerializeTestOutput(response);
            claimed.ComparisonJson = PromptTestRunJson.SerializeComparison(comparison);
            claimed.CompletedUtc = DateTime.UtcNow;
            claimed.PromptTokens = response.Usage?.PromptTokens ?? 0;
            claimed.CompletionTokens = response.Usage?.CompletionTokens ?? 0;
            claimed.TotalTokens = response.Usage?.TotalTokens ?? 0;

            await _testRunRepository.CompleteAsync(claimed, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptTestRunProcessorComponent,
                Consts.ProcessTestRunOperation,
                string.Format(Strings.PromptTestRunCompletedLog, claimed.Id, claimed.PromptVersionId));
        }

        private async Task HandleEngineFailureAsync(PromptTestRun claimed, ReviewResponse response, int maxAttempts)
        {
            string message = string.IsNullOrWhiteSpace(response.Error) ? Strings.UnknownReviewError : response.Error;

            if (claimed.AttemptCount < maxAttempts)
            {
                IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
                TimeSpan delay = policy.GetRetryDelay(claimed.AttemptCount);

                await _testRunRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(delay),
                    message,
                    string.Empty,
                    response.Usage,
                    CancellationToken.None).ConfigureAwait(false);

                _diagnostics.Log(
                    OperationalLogLevel.Warning,
                    Consts.PromptTestRunProcessorComponent,
                    Consts.ProcessTestRunOperation,
                    message,
                    retryAttempt: claimed.AttemptCount);
                return;
            }

            await FailAsync(claimed, message, FailureCategory.Transient, response.Usage).ConfigureAwait(false);
        }

        private async Task HandleUnexpectedAsync(PromptTestRun claimed, Exception exception)
        {
            IRetryPolicy policy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
            RetryDecision decision = policy.Evaluate(claimed.AttemptCount, exception);
            string message = string.IsNullOrWhiteSpace(exception.Message) ? Strings.UnknownReviewError : exception.Message;

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.PromptTestRunProcessorComponent,
                Consts.ProcessTestRunOperation,
                message,
                exception,
                retryAttempt: claimed.AttemptCount);

            if (decision.ShouldRetry)
            {
                await _testRunRepository.ReleaseForRetryAsync(
                    claimed.Id,
                    DateTime.UtcNow.Add(decision.Delay),
                    message,
                    string.Empty,
                    null,
                    CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await FailAsync(claimed, message, decision.Category, null).ConfigureAwait(false);
        }

        private async Task FailAsync(PromptTestRun claimed, string message, FailureCategory category, TokenUsage usage)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? Strings.UnknownReviewError : message;

            await _testRunRepository.FailAsync(claimed.Id, safeMessage, category, string.Empty, usage, DateTime.UtcNow, CancellationToken.None).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Error,
                Consts.PromptTestRunProcessorComponent,
                Consts.ProcessTestRunOperation,
                string.Format(Strings.PromptTestRunFailedLog, claimed.Id, safeMessage),
                retryAttempt: claimed.AttemptCount);
        }

        private async Task<AIEngine> ResolveEngineAsync(CancellationToken cancellationToken)
        {
            AIEngine engine = await _engineRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (engine is not null && engine.Enabled)
            {
                return engine;
            }

            IReadOnlyList<AIEngine> enabledEngines = await _engineRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
            return enabledEngines.Count == 0 ? null : enabledEngines[0];
        }

        private static string BuildTestPrompt(Prompt prompt, PromptVersion version, AIReviewContext original)
        {
            string systemPrompt = original.SystemPrompt ?? string.Empty;
            string reviewPrompt = original.ReviewPrompt ?? string.Empty;

            if (prompt.PromptType is PromptType.System)
            {
                systemPrompt = version.PromptText ?? string.Empty;
            }
            else
            {
                reviewPrompt = version.PromptText ?? string.Empty;
            }

            return BuildCombinedPrompt(systemPrompt, reviewPrompt);
        }

        private static string BuildCombinedPrompt(string systemPrompt, string reviewPrompt)
        {
            StringBuilder builder = new();

            AppendPromptSection(builder, systemPrompt);
            AppendPromptSection(builder, reviewPrompt);
            AppendPromptSection(builder, DefaultReviewContext.ReviewInstructions);

            return builder.ToString();
        }

        private static void AppendPromptSection(StringBuilder builder, string section)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(section);
        }

        private static string BuildOriginalOutputJson(Finding finding, AIReviewContext original)
        {
            if (finding is null)
            {
                return string.Empty;
            }

            return PromptTestRunJson.SerializeOriginalOutput(new PromptTestOriginalOutput
            {
                FindingId = finding.Id,
                Severity = finding.Severity,
                Title = finding.Title ?? string.Empty,
                Description = finding.Description ?? string.Empty,
                SuggestedFix = finding.SuggestedFix ?? string.Empty,
                EngineName = original?.EngineName ?? string.Empty,
                EngineProvider = original?.EngineProvider ?? string.Empty,
                EngineModel = original?.EngineModel ?? string.Empty
            });
        }

        private static PromptTestComparison BuildComparison(Finding finding, AIReviewContext original, AIEngine engine, ReviewResponse response)
        {
            bool originalDetected = finding is not null;
            bool testDetected = response.Results.Count > 0;
            FindingSeverity? highestSeverity = testDetected ? response.Results.Max(result => result.Severity) : null;

            PromptTestComparison comparison = new()
            {
                OriginalIssueDetected = originalDetected,
                TestIssueDetected = testDetected,
                TestIssueCount = response.Results.Count,
                OriginalSeverity = finding?.Severity,
                TestHighestSeverity = highestSeverity,
                MatchesOriginalIssue = finding is not null && response.Results.Any(result => MatchesFinding(result, finding)),
                ContextRequests = response.ContextRequests ?? []
            };

            if (string.IsNullOrWhiteSpace(original?.SystemPrompt) || string.IsNullOrWhiteSpace(original?.ReviewPrompt))
            {
                comparison.Notes.Add(Strings.PromptTestRunPartialPromptSnapshot);
            }

            if (engine is not null && original is not null && !string.Equals(engine.Model, original.EngineModel, StringComparison.Ordinal))
            {
                comparison.Notes.Add(string.Format(Strings.PromptTestRunModelDifference, original.EngineModel, engine.Model));
            }

            return comparison;
        }

        private static bool MatchesFinding(ReviewResult result, Finding finding)
        {
            if (!string.IsNullOrWhiteSpace(finding.Title) && string.Equals(result.Title, finding.Title, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(finding.Description)
                && string.Equals(result.Description, finding.Description, StringComparison.OrdinalIgnoreCase);
        }
    }
}
