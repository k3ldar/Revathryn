using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for discovered repository project structures.</summary>
    public interface IProjectRepository
    {
        /// <summary>Returns all projects belonging to a repository.</summary>
        Task<IReadOnlyList<RepositoryProject>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Reconciles the repository's project structures in a single transaction.</summary>
        Task ReconcileAsync(long repositoryId, IReadOnlyList<RepositoryProject> projects, CancellationToken cancellationToken = default);
    }
}
