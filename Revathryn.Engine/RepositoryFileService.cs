using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>Application-level repository file operations.</summary>
    public sealed class RepositoryFileService : IRepositoryFileService
    {
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepoFileRepository _fileRepository;
        private readonly IRepositoryScanner _scanner;

        /// <summary>Initialises a new service.</summary>
        public RepositoryFileService(
            IRepositoryRepository repositoryRepository,
            IRepoFileRepository fileRepository,
            IRepositoryScanner scanner)
        {
            _repositoryRepository = repositoryRepository;
            _fileRepository = fileRepository;
            _scanner = scanner;
        }

        /// <summary>Returns all files belonging to a repository.</summary>
        public async Task<IReadOnlyList<RepoFile>> GetFilesAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RepoFile> files = await _fileRepository.GetByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
            return [.. files.Where(file => !file.IsRemoved)];
        }

        /// <summary>Returns the file with the given id, or null when not found.</summary>
        public Task<RepoFile> GetFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            return _fileRepository.GetByIdAsync(repoFileId, cancellationToken);
        }

        /// <summary>Marks a file as excluded or included for review.</summary>
        public async Task SetExcludedAsync(long repoFileId, bool isExcluded, CancellationToken cancellationToken = default)
        {
            RepoFile file = await _fileRepository.GetByIdAsync(repoFileId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(string.Format(Strings.RepoFileNotFound, repoFileId));
            file.IsExcluded = isExcluded;
            await _fileRepository.UpdateAsync(file, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Rescans a repository and returns the discovered files.</summary>
        public async Task<IReadOnlyList<RepoFile>> RescanAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            Repository repository = await _repositoryRepository.GetByIdAsync(repositoryId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(string.Format(Strings.RepositoryNotFound, repositoryId));
            IReadOnlyList<RepoFile> existingFiles = await _fileRepository.GetByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
            Dictionary<string, RepoFile> existingFileMap = new(StringComparer.Ordinal);

            foreach (RepoFile file in existingFiles)
            {
                if (!file.IsRemoved)
                {
                    existingFileMap[file.RelativePath] = file;
                }
            }

            RepositoryScanResult scan = await _scanner.ScanAsync(repository.LocalPath, repository.ScanOptions, null, cancellationToken, existingFileMap).ConfigureAwait(false);

            foreach (RepoFile file in scan.DiscoveredFiles)
            {
                file.RepositoryId = repositoryId;
            }

            List<string> failedPaths = [];
            foreach (ScanFailure failure in scan.Failures)
            {
                failedPaths.Add(failure.RelativePath);
            }

            await _fileRepository.ReconcileAsync(repositoryId, scan.DiscoveredFiles, failedPaths, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<RepoFile> files = await _fileRepository.GetByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
            return [.. files.Where(file => !file.IsRemoved)];
        }
    }
}
