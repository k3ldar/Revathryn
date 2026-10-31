using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Application-level repository file operations.</summary>
    public interface IRepositoryFileService
    {
        /// <summary>Returns all files belonging to a repository.</summary>
        Task<IReadOnlyList<RepoFile>> GetFilesAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns the file with the given id, or null when not found.</summary>
        Task<RepoFile> GetFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Marks a file as excluded or included for review.</summary>
        Task SetExcludedAsync(long repoFileId, bool isExcluded, CancellationToken cancellationToken = default);

        /// <summary>Rescans a repository and returns the discovered files.</summary>
        Task<IReadOnlyList<RepoFile>> RescanAsync(long repositoryId, CancellationToken cancellationToken = default);
    }
}
