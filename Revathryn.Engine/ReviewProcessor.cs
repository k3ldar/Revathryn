using System.Security.Cryptography;
using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
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
            IReviewContextProvider contextProvider)
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
        }

        /// <summary>Processes the next pending queue item, or returns false when the queue is empty.</summary>
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            QueueItem item = await _reviewQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);

            if (item is null)
            {
                return false;
            }

            try
            {
                await ProcessItemAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordOperationalFailureAsync(item, exception.Message, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        private async Task ProcessItemAsync(QueueItem item, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryRepository.GetByIdAsync(item.RepositoryId, cancellationToken).ConfigureAwait(false);

            if (repository is null)
            {
                return;
            }

            RepoFile file = await _fileRepository.GetByIdAsync(item.RepoFileId, cancellationToken).ConfigureAwait(false);

            if (file is null)
            {
                return;
            }

            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(item.ReviewTypeId, cancellationToken).ConfigureAwait(false);

            if (reviewType is null || !reviewType.Enabled)
            {
                return;
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

            while (true)
            {
                ReviewResponse response = await _aiReviewEngine.ReviewAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.Success)
                {
                    await RecordOperationalFailureAsync(item, response.Error, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (response.ContextRequests is null || response.ContextRequests.Count == 0)
                {
                    if (response.Results is null)
                    {
                        await RecordOperationalFailureAsync(item, Strings.AIEngineResponseIncomplete, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    await PersistOutcomeAsync(item, file, reviewType, response, cancellationToken).ConfigureAwait(false);
                    return;
                }

                contextIterations++;

                if (contextIterations > maxContextRequests)
                {
                    await RecordOperationalFailureAsync(item, Strings.ContextRequestLimitExceeded, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await AppendContextAsync(repository, file, request, response.ContextRequests, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task PersistOutcomeAsync(
            QueueItem item,
            RepoFile file,
            ReviewType reviewType,
            ReviewResponse response,
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

            if (string.IsNullOrWhiteSpace(masterPrompt))
            {
                return reviewType.Prompt ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(reviewType.Prompt))
            {
                return masterPrompt;
            }

            return string.Concat(masterPrompt, Environment.NewLine, Environment.NewLine, reviewType.Prompt);
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
    }
}
