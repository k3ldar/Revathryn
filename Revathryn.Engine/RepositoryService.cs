using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>Application-level repository management operations.</summary>
    public sealed class RepositoryService : IRepositoryService
    {
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IReviewTypeSeeder _reviewTypeSeeder;
        private readonly IRepositoryProcessor _repositoryProcessor;

        /// <summary>Initialises a new service.</summary>
        public RepositoryService(IRepositoryRepository repositoryRepository, IReviewTypeSeeder reviewTypeSeeder, IRepositoryProcessor repositoryProcessor)
        {
            _repositoryRepository = repositoryRepository;
            _reviewTypeSeeder = reviewTypeSeeder;
            _repositoryProcessor = repositoryProcessor;
        }

        /// <summary>Registers a local repository, seeds its built-in review definitions, and triggers an initial review when automatic review is enabled.</summary>
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

            await _reviewTypeSeeder.LinkAllReviewTypesAsync(id, cancellationToken).ConfigureAwait(false);

            TriggerInitialReview(repository);

            return repository;
        }

        /// <summary>Updates an existing repository.</summary>
        public async Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            ValidateLocalPath(repository.LocalPath);

            string normalisedPath = NormalisePath(repository.LocalPath);
            repository.LocalPath = normalisedPath;

            await EnsureNotDuplicateAsync(normalisedPath, cancellationToken, repository.Id).ConfigureAwait(false);

            repository.LastModifiedDate = DateTime.UtcNow;
            await _repositoryRepository.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
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

        private async Task EnsureNotDuplicateAsync(string normalisedPath, CancellationToken cancellationToken, long? excludeId = null)
        {
            IReadOnlyList<Repository> existing = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            foreach (Repository repository in existing)
            {
                if (excludeId.HasValue && repository.Id == excludeId.Value)
                {
                    continue;
                }

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

        private void TriggerInitialReview(Repository repository)
        {
            if (!repository.Enabled || !repository.AutomaticReviewEnabled)
            {
                return;
            }

            _ = ProcessInitialReviewAsync(repository.Id);
        }

        private async Task ProcessInitialReviewAsync(long repositoryId)
        {
            try
            {
                await _repositoryProcessor.ProcessAsync(repositoryId, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort background processing must not fail registration.
            }
        }
    }
}