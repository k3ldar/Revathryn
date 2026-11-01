using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Application-level repository management operations.</summary>
    public interface IRepositoryService
    {
        /// <summary>Registers a local repository, copies default review definitions into it, and triggers an initial review when automatic review is enabled.</summary>
        Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing repository.</summary>
        Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default);

        /// <summary>Deletes a repository and its dependent data.</summary>
        Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns the repository with the given id, or null when not found.</summary>
        Task<Repository> GetAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns all repositories.</summary>
        Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Enables or disables a repository.</summary>
        Task SetEnabledAsync(long repositoryId, bool enabled, CancellationToken cancellationToken = default);
    }
}
