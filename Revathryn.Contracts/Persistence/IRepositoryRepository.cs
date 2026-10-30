using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for registered repositories.</summary>
    public interface IRepositoryRepository
    {
        /// <summary>Returns the repository with the given id, or null when not found.</summary>
        Task<Repository> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all repositories.</summary>
        Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns all enabled repositories.</summary>
        Task<IReadOnlyList<Repository>> GetEnabledAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts the repository and returns its assigned id.</summary>
        Task<long> AddAsync(Repository repository, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing repository.</summary>
        Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default);

        /// <summary>Deletes the repository with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
