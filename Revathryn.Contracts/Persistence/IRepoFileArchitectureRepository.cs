using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for cached per-file architecture summaries.</summary>
    public interface IRepoFileArchitectureRepository
    {
        /// <summary>Returns the cached summary for a file, or null when not found.</summary>
        Task<RepoFileArchitecture> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Inserts or updates the cached summary and returns its id.</summary>
        Task<long> UpsertAsync(RepoFileArchitecture architecture, CancellationToken cancellationToken = default);

        /// <summary>Deletes the cached summary for a file.</summary>
        Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Deletes all cached summaries belonging to a repository.</summary>
        Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);
    }
}
