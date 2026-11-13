using Revathryn.Contracts.Infrastructure;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Resolves the currently checked-out git branch by reading the repository's <c>.git/HEAD</c> file.</summary>
    public sealed class GitBranchProvider : IGitBranchProvider
    {
        /// <inheritdoc/>
        public async Task<string> GetCurrentBranchAsync(string repositoryLocalPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repositoryLocalPath))
            {
                return string.Empty;
            }

            string headPath = Path.Combine(repositoryLocalPath, FilePathGit, GitHeadFileName);

            string content;
            try
            {
                content = await File.ReadAllTextAsync(headPath, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }

            string trimmed = content.Trim();

            if (!trimmed.StartsWith(GitHeadRefPrefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return trimmed.Substring(GitHeadRefPrefix.Length).Trim();
        }
    }
}
