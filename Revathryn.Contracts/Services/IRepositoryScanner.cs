using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Discovers files on disk for a repository.</summary>
    public interface IRepositoryScanner
    {
        /// <summary>Scans a local folder and returns the files discovered beneath it.</summary>
        /// <param name="localPath">The root folder to scan.</param>
        /// <param name="options">Optional scanning rules; defaults are used when null.</param>
        /// <param name="progress">Optional progress reporting callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Discovered files and any files that could not be fingerprinted.</returns>
        Task<RepositoryScanResult> ScanAsync(string localPath, RepositoryScanOptions options = null, IProgress<RepositoryScanProgress> progress = null, CancellationToken cancellationToken = default);
    }
}
