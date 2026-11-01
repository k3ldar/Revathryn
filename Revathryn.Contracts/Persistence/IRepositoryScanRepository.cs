using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for repository scan execution records.</summary>
    public interface IRepositoryScanRepository
    {
        /// <summary>Inserts a scan record and returns its assigned id.</summary>
        Task<long> AddAsync(RepositoryScanRecord record, CancellationToken cancellationToken = default);

        /// <summary>Returns scan records for a repository, most recent first.</summary>
        Task<IReadOnlyList<RepositoryScanRecord>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns all scan records across repositories, most recent first.</summary>
        Task<IReadOnlyList<RepositoryScanRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
