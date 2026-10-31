using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for completed file analysis records.</summary>
    public interface IRepoFileAnalysisRepository
    {
        /// <summary>Returns the analysis record for the file and review type, or null when not found.</summary>
        Task<RepoFileAnalysis> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns all analysis records belonging to a repository.</summary>
        Task<IReadOnlyList<RepoFileAnalysis>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Inserts or updates the analysis record and returns its id.</summary>
        Task<long> UpsertAsync(RepoFileAnalysis analysis, CancellationToken cancellationToken = default);

        /// <summary>Deletes the analysis record with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Deletes all analysis records for a file.</summary>
        Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);
    }
}
