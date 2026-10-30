using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for discovered repository files.</summary>
    public interface IRepoFileRepository
    {
        /// <summary>Returns the file with the given id, or null when not found.</summary>
        Task<RepoFile> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all files belonging to a repository.</summary>
        Task<IReadOnlyList<RepoFile>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns the file with the given repository-relative path, or null when not found.</summary>
        Task<RepoFile> GetByRelativePathAsync(long repositoryId, string relativePath, CancellationToken cancellationToken = default);

        /// <summary>Inserts or updates the file and returns its id.</summary>
        Task<long> UpsertAsync(RepoFile file, CancellationToken cancellationToken = default);

        /// <summary>Reconciles discovered files against the repository inventory atomically.</summary>
        /// <param name="repositoryId">The repository being reconciled.</param>
        /// <param name="discoveredFiles">Files discovered and fingerprinted by the current scan.</param>
        /// <param name="failedPaths">Paths that could not be scanned and must retain their previous state.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Counts describing how the inventory changed.</returns>
        Task<RepositoryReconciliationResult> ReconcileAsync(
            long repositoryId,
            IReadOnlyList<RepoFile> discoveredFiles,
            IReadOnlyCollection<string> failedPaths,
            CancellationToken cancellationToken = default);

        /// <summary>Updates an existing file.</summary>
        Task UpdateAsync(RepoFile file, CancellationToken cancellationToken = default);

        /// <summary>Deletes the file with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Deletes all files belonging to a repository.</summary>
        Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);
    }
}
