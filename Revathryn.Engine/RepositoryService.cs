using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Application-level repository management operations.</summary>
    public sealed class RepositoryService : IRepositoryService
    {
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;

        /// <summary>Initialises a new service.</summary>
        public RepositoryService(IRepositoryRepository repositoryRepository, IReviewTypeRepository reviewTypeRepository)
        {
            _repositoryRepository = repositoryRepository;
            _reviewTypeRepository = reviewTypeRepository;
        }

        /// <summary>Registers a local repository and copies default review definitions into it.</summary>
        public async Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            ValidateLocalPath(repository.LocalPath);

            string normalisedPath = NormalisePath(repository.LocalPath);
            repository.LocalPath = normalisedPath;

            await EnsureNotDuplicateAsync(normalisedPath, cancellationToken).ConfigureAwait(false);

            DateTime now = DateTime.UtcNow;
            repository.CreatedDate = now;
            repository.LastModifiedDate = now;

            long id = await _repositoryRepository.AddAsync(repository, cancellationToken).ConfigureAwait(false);
            repository.Id = id;

            foreach (ReviewType defaultType in CreateDefaultReviewTypes(id))
            {
                await _reviewTypeRepository.AddAsync(defaultType, cancellationToken).ConfigureAwait(false);
            }

            return repository;
        }

        /// <summary>Updates an existing repository.</summary>
        public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            repository.LastModifiedDate = DateTime.UtcNow;
            return _repositoryRepository.UpdateAsync(repository, cancellationToken);
        }

        /// <summary>Deletes a repository and its dependent data.</summary>
        public Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            return _repositoryRepository.DeleteAsync(repositoryId, cancellationToken);
        }

        /// <summary>Returns the repository with the given id, or null when not found.</summary>
        public Task<Repository> GetAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            return _repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
        }

        /// <summary>Returns all repositories.</summary>
        public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return _repositoryRepository.GetAllAsync(cancellationToken);
        }

        /// <summary>Enables or disables a repository.</summary>
        public async Task SetEnabledAsync(long repositoryId, bool enabled, CancellationToken cancellationToken = default)
        {
            Repository repository = await _repositoryRepository.GetByIdAsync(repositoryId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(string.Format(Strings.RepositoryNotFound, repositoryId));
            repository.Enabled = enabled;
            await _repositoryRepository.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
        }

        private async Task EnsureNotDuplicateAsync(string normalisedPath, CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> existing = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            foreach (Repository repository in existing)
            {
                if (string.Equals(NormalisePath(repository.LocalPath), normalisedPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format(Strings.RepositoryAlreadyRegistered, normalisedPath));
                }
            }
        }

        private static void ValidateLocalPath(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException(Strings.LocalRepositoryPathRequired, nameof(localPath));
            }

            if (!Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException(string.Format(Strings.RepositoryFolderNotFound, localPath));
            }

            try
            {
                _ = Directory.GetFileSystemEntries(localPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new InvalidOperationException(string.Format(Strings.RepositoryFolderNotAccessible, localPath), ex);
            }
        }

        private static string NormalisePath(string localPath)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath));
        }

        private static IReadOnlyList<ReviewType> CreateDefaultReviewTypes(long repositoryId)
        {
            DateTime now = DateTime.UtcNow;

            ReviewType cSharpReview = new()
            {
                RepositoryId = repositoryId,
                Name = Strings.DefaultCSharpReviewName,
                Description = Strings.DefaultCSharpReviewDescription,
                FileExtensions = FileTypeCSharp,
                Prompt = Strings.DefaultCSharpReviewPrompt,
                DefaultPriority = DefaultCSharpReviewPriority,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };

            ReviewType generalReview = new()
            {
                RepositoryId = repositoryId,
                Name = Strings.DefaultGeneralReviewName,
                Description = Strings.DefaultGeneralReviewDescription,
                FileExtensions = string.Empty,
                Prompt = Strings.DefaultGeneralReviewPrompt,
                DefaultPriority = DefaultGeneralReviewPriority,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };

            cSharpReview.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(cSharpReview);
            generalReview.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(generalReview);

            return [cSharpReview, generalReview];
        }
    }
}