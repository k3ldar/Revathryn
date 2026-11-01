using System.Security.Cryptography;
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
    /// Processes queued review operations: loads the file, executes the review
    /// engine with optional context retrieval, persists findings and records a
    /// successful outcome or a distinguishable operational failure.
    /// </summary>
    public sealed class ReviewProcessor : IReviewProcessor
    {
        private readonly IReviewQueue _reviewQueue;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepoFileRepository _fileRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IReviewOutcomeStore _reviewOutcomeStore;
        private readonly IFindingRepository _findingRepository;
        private readonly IAISettingsRepository _aiSettingsRepository;
        private readonly IAIReviewEngine _aiReviewEngine;
        private readonly IReviewContextProvider _contextProvider;
        private readonly IRetryPolicyProvider _retryPolicyProvider;
        private readonly IReviewUsageRepository _reviewUsageRepository;
        private readonly ResilienceOptions _resilienceOptions;

        /// <summary>Initialises a new processor.</summary>
        public ReviewProcessor(
            IReviewQueue reviewQueue,
            IRepositoryRepository repositoryRepository,
            IRepoFileRepository fileRepository,
            IReviewTypeRepository reviewTypeRepository,
            IReviewOutcomeStore reviewOutcomeStore,
            IFindingRepository findingRepository,
            IAISettingsRepository aiSettingsRepository,
            IAIReviewEngine aiReviewEngine,
            IReviewContextProvider contextProvider,
            IRetryPolicyProvider retryPolicyProvider,
            IReviewUsageRepository reviewUsageRepository,
            ResilienceOptions resilienceOptions)
        {
            _reviewQueue = reviewQueue;
            _repositoryRepository = repositoryRepository;
            _fileRepository = fileRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _reviewOutcomeStore = reviewOutcomeStore;
            _findingRepository = findingRepository;
            _aiSettingsRepository = aiSettingsRepository;
            _aiReviewEngine = aiReviewEngine;
            _contextProvider = contextProvider;
            _retryPolicyProvider = retryPolicyProvider;
            _reviewUsageRepository = reviewUsageRepository;
            _resilienceOptions = resilienceOptions;
        }

        /// <summary>Processes the next eligible queue item, or returns false when the queue is empty.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            DateTime leasedUntilUtc = DateTime.UtcNow.Add(_resilienceOptions.QueueLeaseDuration);
            QueueItem item = await _reviewQueue.ClaimNextAsync(leasedUntilUtc, cancellationToken).ConfigureAwait(false);

            if (item is null)
            {
                return false;
            }

            try
            {
                ProcessingResult result = await ProcessItemAsync(item, cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    await _reviewQueue.MarkCompletedAsync(item.Id, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await HandleFailureAsync(item, result.Error, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _reviewQueue.ReleaseForRetryAsync(item.Id, item.Priority, DateTime.UtcNow, Strings.ReviewCancelled, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                await HandleFailureAsync(item, exception.Message, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        private async Task<ProcessingResult> ProcessItemAsync(QueueItem item, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryRepository.GetByIdAsync(item.RepositoryId, cancellationToken).ConfigureAwait(false);

            if (repository is null)
            {
                return ProcessingResult.Failed(string.Format(Strings.RepositoryNotFound, item.RepositoryId));
            }

            RepoFile file = await _fileRepository.GetByIdAsync(item.RepoFileId, cancellationToken).ConfigureAwait(false);

            if (file is null)
            {
                return ProcessingResult.Failed(string.Format(Strings.RepoFileNotFound, item.RepoFileId));
            }

            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(item.ReviewTypeId, cancellationToken).ConfigureAwait(false);

            if (reviewType is null || !reviewType.Enabled)
            {
                return ProcessingResult.Failed(Strings.ReviewTypeNotAvailable);
            }

            string fileContent = await File.ReadAllTextAsync(ResolveAbsolutePath(repository.LocalPath, file.RelativePath), cancellationToken).ConfigureAwait(false);

            ReviewRequest request = new()
            {
                RepositoryId = item.RepositoryId,
                RepoFileId = item.RepoFileId,
                ReviewTypeId = item.ReviewTypeId,
                Language = ResolveLanguage(file),
                FileContent = fileContent,
                Prompt = await BuildPromptAsync(reviewType, cancellationToken).ConfigureAwait(false)
            };

            int maxContextRequests = repository.MaxContextRequests > 0 ? repository.MaxContextRequests : Consts.DefaultMaxContextRequests;
            int contextIterations = 0;
            List<ReviewUsage> usageRecords = [];

            while (true)
            {
                ReviewResponse response = await _aiReviewEngine.ReviewAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.Success)
                {
                    await PersistUsageRecordAsync(item, response, cancellationToken).ConfigureAwait(false);
                    return ProcessingResult.Failed(response.Error);
                }

                usageRecords.Add(BuildUsageRecord(item, response));

                if (response.ContextRequests is null || response.ContextRequests.Count == 0)
                {
                    if (response.Results is null)
                    {
                        return ProcessingResult.Failed(Strings.AIEngineResponseIncomplete);
                    }

                    await PersistOutcomeAsync(item, file, reviewType, response, usageRecords, cancellationToken).ConfigureAwait(false);
                    return ProcessingResult.Success();
                }

                contextIterations++;

                if (contextIterations > maxContextRequests)
                {
                    return ProcessingResult.Failed(Strings.ContextRequestLimitExceeded);
                }

                await AppendContextAsync(repository, file, request, response.ContextRequests, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task HandleFailureAsync(QueueItem item, string error, CancellationToken cancellationToken)
        {
            string message = string.IsNullOrWhiteSpace(error) ? Strings.UnknownReviewError : error;
            RetryPolicyOptions reviewPolicy = _resilienceOptions.GetRetryPolicy(RetryOperationType.ExternalService);
            int maxAttempts = item.MaxAttempts > 0 ? item.MaxAttempts : reviewPolicy.MaxAttempts;

            if (item.AttemptCount >= maxAttempts)
            {
                await RecordOperationalFailureAsync(item, message, cancellationToken).ConfigureAwait(false);
                await _reviewQueue.RemoveAsync(item.Id, cancellationToken).ConfigureAwait(false);
                return;
            }

            IRetryPolicy retryPolicy = _retryPolicyProvider.GetPolicy(RetryOperationType.ExternalService);
            TimeSpan delay = retryPolicy.GetRetryDelay(item.AttemptCount);
            DateTime nextAttemptUtc = DateTime.UtcNow.Add(delay);
            int priority = DecayPriority(item.Priority);

            await _reviewQueue.ReleaseForRetryAsync(item.Id, priority, nextAttemptUtc, message, cancellationToken).ConfigureAwait(false);
        }

        private int DecayPriority(int priority)
        {
            int factor = Math.Max(Consts.MinimumPriorityDecayFactor, _resilienceOptions.QueuePriorityDecayFactor);
            int decayed = priority / factor;

            return Math.Max(_resilienceOptions.QueueMinimumPriority, decayed);
        }

        private async Task PersistOutcomeAsync(
            QueueItem item,
            RepoFile file,
            ReviewType reviewType,
            ReviewResponse response,
            IReadOnlyList<ReviewUsage> usageRecords,
            CancellationToken cancellationToken)
        {
            List<Finding> findings = [];

            foreach (ReviewResult result in response.Results ?? [])
            {
                Finding finding = new()
                {
                    RepositoryId = item.RepositoryId,
                    RepoFileId = item.RepoFileId,
                    ReviewTypeId = item.ReviewTypeId,
                    Fingerprint = BuildFindingFingerprint(item, result),
                    Severity = result.Severity,
                    Title = result.Title ?? string.Empty,
                    Description = result.Description ?? string.Empty,
                    SuggestedFix = result.SuggestedFix ?? string.Empty,
                    Status = FindingStatus.New,
                    Category = FindingCategory.CodeIssue,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };

                findings.Add(finding);
            }

            ReviewOutcome outcome = new()
            {
                RepositoryId = item.RepositoryId,
                RepoFileId = item.RepoFileId,
                ReviewTypeId = item.ReviewTypeId,
                Sha256 = file.Sha256,
                AnalysisVersion = Consts.AnalysisVersion,
                ReviewDefinitionVersion = reviewType.ReviewDefinitionVersion,
                Findings = findings,
                UsageRecords = usageRecords,
                CompletedUtc = DateTime.UtcNow
            };

            await _reviewOutcomeStore.PersistAsync(outcome, cancellationToken).ConfigureAwait(false);
        }

        private async Task RecordOperationalFailureAsync(QueueItem item, string error, CancellationToken cancellationToken)
        {
            string message = string.IsNullOrWhiteSpace(error) ? Strings.UnknownReviewError : error;

            Finding finding = new()
            {
                RepositoryId = item.RepositoryId,
                RepoFileId = item.RepoFileId,
                ReviewTypeId = item.ReviewTypeId,
                Fingerprint = BuildOperationalFailureFingerprint(item),
                Severity = FindingSeverity.High,
                Title = Strings.OperationalFailureTitle,
                Description = string.Format(Strings.OperationalFailureDescription, item.RepoFileId, item.ReviewTypeId, message),
                SuggestedFix = Strings.OperationalFailureSuggestedFix,
                Status = FindingStatus.New,
                Category = FindingCategory.OperationalFailure,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            await _findingRepository.AddAsync(finding, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> BuildPromptAsync(ReviewType reviewType, CancellationToken cancellationToken)
        {
            AISettings settings = await _aiSettingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
            string masterPrompt = settings?.MasterPrompt ?? string.Empty;
            string taskPrompt = reviewType.Prompt ?? string.Empty;

            StringBuilder prompt = new();

            AppendPromptSection(prompt, masterPrompt);
            AppendPromptSection(prompt, taskPrompt);
            AppendPromptSection(prompt, DefaultReviewContext.ReviewInstructions);

            return prompt.ToString();
        }

        private static void AppendPromptSection(StringBuilder prompt, string section)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                return;
            }

            if (prompt.Length > 0)
            {
                prompt.AppendLine();
                prompt.AppendLine();
            }

            prompt.Append(section);
        }

        private async Task AppendContextAsync(
            Repository repository,
            RepoFile file,
            ReviewRequest request,
            IReadOnlyList<string> contextRequests,
            CancellationToken cancellationToken)
        {
            string sourcePath = ResolveAbsolutePath(repository.LocalPath, file.RelativePath);

            foreach (string contextKey in contextRequests.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ReviewContextRequest contextRequest = new()
                {
                    RepositoryId = request.RepositoryId,
                    RepositoryName = repository.Name,
                    RepoFileId = request.RepoFileId,
                    Language = request.Language,
                    ContextKey = contextKey,
                    MaxItems = Consts.MaxContextItemsPerRequest,
                    SourcePath = sourcePath
                };

                ReviewContextResponse contextResponse = await _contextProvider.GetContextAsync(contextRequest, cancellationToken).ConfigureAwait(false);

                foreach (ContextItem item in contextResponse?.Items ?? [])
                {
                    request.AdditionalContext.Add(RenderContextItem(item));
                }
            }
        }

        private static string RenderContextItem(ContextItem item)
        {
            string title = string.IsNullOrWhiteSpace(item.Title) ? string.Empty : item.Title;
            string content = item.Content ?? string.Empty;

            if (content.Length > Consts.MaxContextItemContentLength)
            {
                content = content[..Consts.MaxContextItemContentLength];
            }

            return string.IsNullOrWhiteSpace(title)
                ? content
                : string.Concat(Consts.CharLeftSquareBracket, title, Consts.CharRightSquareBracket, Environment.NewLine, content);
        }

        private static ReviewUsage BuildUsageRecord(ReviewResponse response)
        {
            TokenUsage usage = response.Usage ?? new TokenUsage();

            return new ReviewUsage
            {
                EngineId = response.EngineId,
                EngineName = response.EngineName ?? string.Empty,
                EngineModel = response.EngineModel ?? string.Empty,
                AttemptCount = usage.AttemptCount,
                PromptTokens = usage.PromptTokens,
                CompletionTokens = usage.CompletionTokens,
                TotalTokens = usage.TotalTokens,
                CachedTokens = usage.CachedTokens,
                OccurredUtc = DateTime.UtcNow
            };
        }

        private static ReviewUsage BuildUsageRecord(QueueItem item, ReviewResponse response)
        {
            ReviewUsage usage = BuildUsageRecord(response);
            usage.RepositoryId = item.RepositoryId;
            usage.RepoFileId = item.RepoFileId;
            usage.ReviewTypeId = item.ReviewTypeId;
            return usage;
        }

        private async Task PersistUsageRecordAsync(QueueItem item, ReviewResponse response, CancellationToken cancellationToken)
        {
            await _reviewUsageRepository.AddAsync(BuildUsageRecord(item, response), cancellationToken).ConfigureAwait(false);
        }

        private static string BuildFindingFingerprint(QueueItem item, ReviewResult result)
        {
            return string.Join(
                Consts.FindingFingerprintSeparator,
                item.RepoFileId.ToString(),
                item.ReviewTypeId.ToString(),
                NormaliseResultFingerprint(result));
        }

        private static string BuildOperationalFailureFingerprint(QueueItem item)
        {
            return string.Join(
                Consts.FindingFingerprintSeparator,
                item.RepoFileId.ToString(),
                item.ReviewTypeId.ToString(),
                Consts.OperationalFailureFingerprintSuffix);
        }

        private static string NormaliseResultFingerprint(ReviewResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Fingerprint))
            {
                return result.Fingerprint;
            }

            string composite = string.Join(
                Consts.FindingFingerprintSeparator,
                ((int)result.Severity).ToString(),
                result.Title ?? string.Empty,
                result.Description ?? string.Empty,
                result.SuggestedFix ?? string.Empty);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string ResolveLanguage(RepoFile file)
        {
            return string.Equals(file.Extension, Consts.FileTypeCSharp, StringComparison.OrdinalIgnoreCase)
                ? Consts.LanguageType
                : file.Extension;
        }

        private static string ResolveAbsolutePath(string repositoryLocalPath, string relativePath)
        {
            string osRelativePath = relativePath.Replace(Consts.CharForwardSlash, Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repositoryLocalPath, osRelativePath));
        }

        private sealed class ProcessingResult
        {
            public bool Succeeded { get; init; }

            public string Error { get; init; } = string.Empty;

            public static ProcessingResult Success()
            {
                return new ProcessingResult { Succeeded = true };
            }

            public static ProcessingResult Failed(string error)
            {
                return new ProcessingResult { Succeeded = false, Error = error ?? string.Empty };
            }
        }
    }
}
