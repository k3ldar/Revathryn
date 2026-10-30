using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Discovers project and solution structures within a local repository.</summary>
    public interface IProjectDiscoveryService
    {
        /// <summary>Returns solutions and projects discovered beneath the given root.</summary>
        /// <param name="localPath">The root folder to scan.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The discovered solution and project structures.</returns>
        Task<IReadOnlyList<RepositoryProject>> DiscoverAsync(string localPath, CancellationToken cancellationToken = default);
    }
}
