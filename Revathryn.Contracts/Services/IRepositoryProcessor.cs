using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Coordinates a full repository scan: discovery, fingerprinting, file
    /// reconciliation, C# analysis, review scheduling and queue population.
    /// </summary>
    public interface IRepositoryProcessor
    {
        /// <summary>Scans and processes a single repository, returning a summary of the work performed.</summary>
        /// <param name="repositoryId">The repository to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional progress reporting callback.</param>
        Task<RepositoryScanSummary> ProcessAsync(long repositoryId, CancellationToken cancellationToken, IProgress<RepositoryScanProgress> progress = null);

        /// <summary>Scans and processes every enabled repository, returning a summary per repository.</summary>
        Task<IReadOnlyList<RepositoryScanSummary>> ProcessAllEnabledAsync(CancellationToken cancellationToken);
    }
}
