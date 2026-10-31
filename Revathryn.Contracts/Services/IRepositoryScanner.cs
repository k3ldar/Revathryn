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
        /// <param name="existingFiles">Optional prior inventory, keyed by repository-relative path, used to skip re-hashing unchanged files.</param>
        /// <param name="forceFullScan">When true, every file is fingerprinted regardless of prior inventory metadata.</param>
        /// <returns>Discovered files and any files that could not be fingerprinted.</returns>
        Task<RepositoryScanResult> ScanAsync(
            string localPath,
            RepositoryScanOptions options = null,
            IProgress<RepositoryScanProgress> progress = null,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, RepoFile> existingFiles = null,
            bool forceFullScan = false);
    }
}
