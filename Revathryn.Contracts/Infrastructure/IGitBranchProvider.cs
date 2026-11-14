namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Resolves the currently checked-out git branch for a repository working copy.</summary>
    public interface IGitBranchProvider
    {
        /// <summary>
        /// Returns the name of the branch currently checked out at <paramref name="repositoryLocalPath"/>,
        /// or <see cref="string.Empty"/> when the working copy is in detached HEAD state or the branch cannot be determined.
        /// </summary>
        /// <param name="repositoryLocalPath">Absolute path of the repository working copy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The branch name, or <see cref="string.Empty"/> when unknown or detached.</returns>
        Task<string> GetCurrentBranchAsync(string repositoryLocalPath, CancellationToken cancellationToken = default);
    }
}
