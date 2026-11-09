using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Schedules review work by matching repository files to review types.</summary>
    public sealed class ReviewScheduler : IReviewScheduler
    {
        private const string CurrentAnalysisVersion = Consts.AnalysisVersion;

        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepoFileRepository _fileRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IReviewLinkRepository _reviewLinkRepository;
        private readonly IRepoFileAnalysisRepository _analysisRepository;
        private readonly IReviewQueue _reviewQueue;

        /// <summary>Initialises a new scheduler.</summary>
        public ReviewScheduler(
            IRepositoryRepository repositoryRepository,
            IRepoFileRepository fileRepository,
            IReviewTypeRepository reviewTypeRepository,
            IReviewLinkRepository reviewLinkRepository,
            IRepoFileAnalysisRepository analysisRepository,
            IReviewQueue reviewQueue)
        {
            _repositoryRepository = repositoryRepository;
            _fileRepository = fileRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _reviewLinkRepository = reviewLinkRepository;
            _analysisRepository = analysisRepository;
            _reviewQueue = reviewQueue;
        }

        /// <summary>Enqueues review work for a whole repository based on its review types.</summary>
        public async Task<ReviewScheduleResult> ScheduleRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            ReviewScheduleResult result = new();
            Repository repository = await _repositoryRepository.GetByIdAsync(repositoryId, cancellationToken).ConfigureAwait(false);

            if (repository is null || !repository.Enabled)
            {
                return result;
            }

            IReadOnlyList<RepoFile> files = await _fileRepository.GetByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetEnabledByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);

            foreach (RepoFile file in files)
            {
                if (file.IsExcluded || file.IsRemoved)
                {
                    continue;
                }

                ReviewScheduleResult fileResult = await ScheduleForFileAsync(repositoryId, file, reviewTypes, cancellationToken).ConfigureAwait(false);
                result.Added += fileResult.Added;
                result.DuplicatesSkipped += fileResult.DuplicatesSkipped;
            }

            return result;
        }

        /// <summary>Enqueues review work for a single file based on applicable review types.</summary>
        public async Task<ReviewScheduleResult> ScheduleFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            ReviewScheduleResult result = new();
            RepoFile file = await _fileRepository.GetByIdAsync(repoFileId, cancellationToken).ConfigureAwait(false);

            if (file is null || file.IsExcluded || file.IsRemoved)
            {
                return result;
            }

            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetEnabledByRepositoryAsync(file.RepositoryId, cancellationToken).ConfigureAwait(false);
            return await ScheduleForFileAsync(file.RepositoryId, file, reviewTypes, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Requeues a single file for a specific review type, forcing re-review even when its analysis is current.</summary>
        public async Task<ReviewScheduleResult> RequeueFileAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
        {
            ReviewScheduleResult result = new();
            RepoFile file = await _fileRepository.GetByIdAsync(repoFileId, cancellationToken).ConfigureAwait(false);

            if (file is null || file.IsExcluded || file.IsRemoved)
            {
                return result;
            }

            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(reviewTypeId, cancellationToken).ConfigureAwait(false);

            if (reviewType is null || !reviewType.Enabled || reviewType.RepositoryId != file.RepositoryId)
            {
                return result;
            }

            if (!AppliesToFile(reviewType, file))
            {
                return result;
            }

            ReviewLink link = await _reviewLinkRepository.GetAsync(file.Id, reviewType.Id, cancellationToken).ConfigureAwait(false);

            if (link is not null && !link.Enabled)
            {
                return result;
            }

            QueueItem item = new()
            {
                RepositoryId = file.RepositoryId,
                RepoFileId = file.Id,
                ReviewTypeId = reviewType.Id,
                Priority = link?.PriorityOverride ?? reviewType.DefaultPriority,
                CreatedDate = DateTime.UtcNow
            };

            if (await _reviewQueue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false))
            {
                result.Added++;
            }
            else
            {
                result.DuplicatesSkipped++;
            }

            return result;
        }

        /// <summary>Returns the enabled review types that apply to the file, respecting disabled links.</summary>
        public async Task<IReadOnlyList<ReviewType>> GetApplicableReviewTypesAsync(RepoFile file, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (file.IsExcluded || file.IsRemoved)
            {
                return [];
            }

            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetEnabledByRepositoryAsync(file.RepositoryId, cancellationToken).ConfigureAwait(false);
            List<ReviewType> applicable = [];

            foreach (ReviewType reviewType in reviewTypes)
            {
                if (!AppliesToFile(reviewType, file))
                {
                    continue;
                }

                ReviewLink link = await _reviewLinkRepository.GetAsync(file.Id, reviewType.Id, cancellationToken).ConfigureAwait(false);

                if (link is not null && !link.Enabled)
                {
                    continue;
                }

                applicable.Add(reviewType);
            }

            return applicable;
        }

        /// <summary>Determines whether the review type already has a current completed analysis for the file.</summary>
        public async Task<bool> IsCurrentAsync(RepoFile file, ReviewType reviewType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(reviewType);

            return !await RequiresReviewAsync(file, reviewType, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReviewScheduleResult> ScheduleForFileAsync(
            long repositoryId,
            RepoFile file,
            IReadOnlyList<ReviewType> reviewTypes,
            CancellationToken cancellationToken)
        {
            ReviewScheduleResult result = new();

            foreach (ReviewType reviewType in reviewTypes)
            {
                if (!AppliesToFile(reviewType, file))
                {
                    continue;
                }

                ReviewLink link = await _reviewLinkRepository.GetAsync(file.Id, reviewType.Id, cancellationToken).ConfigureAwait(false);

                if (link is not null && !link.Enabled)
                {
                    continue;
                }

                if (!await RequiresReviewAsync(file, reviewType, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                QueueItem item = new()
                {
                    RepositoryId = repositoryId,
                    RepoFileId = file.Id,
                    ReviewTypeId = reviewType.Id,
                    Priority = link?.PriorityOverride ?? reviewType.DefaultPriority,
                    CreatedDate = DateTime.UtcNow
                };

                bool added = await _reviewQueue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);

                if (added)
                {
                    result.Added++;
                }
                else
                {
                    result.DuplicatesSkipped++;
                }
            }

            return result;
        }

        private async Task<bool> RequiresReviewAsync(RepoFile file, ReviewType reviewType, CancellationToken cancellationToken)
        {
            RepoFileAnalysis analysis = await _analysisRepository.GetAsync(file.Id, reviewType.Id, cancellationToken).ConfigureAwait(false);

            if (analysis is null)
            {
                return true;
            }

            if (!string.Equals(analysis.Sha256, file.Sha256, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(analysis.ReviewDefinitionVersion, reviewType.ReviewDefinitionVersion, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.Equals(analysis.AnalysisVersion, CurrentAnalysisVersion, StringComparison.Ordinal);
        }

        private static bool AppliesToFile(ReviewType reviewType, RepoFile file)
        {
            string extensions = reviewType.FileExtensions.Trim();

            if (extensions.Length == 0)
            {
                return true;
            }

            string[] parts = extensions.Split(CharComma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string part in parts)
            {
                string extension = part.TrimStart(CharFullStop);

                if (string.Equals(extension, file.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}