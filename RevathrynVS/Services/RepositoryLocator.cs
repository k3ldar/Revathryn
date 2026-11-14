using System.IO;

namespace RevathrynVS2026.Services
{
    /// <summary>Finds the name of the Git repository that contains a file.</summary>
    internal static class RepositoryLocator
    {
        #region Public Methods
        /// <summary>Walks up from <paramref name="filePath"/> looking for a Git repository root.</summary>
        /// <param name="filePath">The full path of the file.</param>
        /// <returns>The repository folder name, or <see langword="null"/> when the file is not in a repository.</returns>
        public static string FindRepositoryName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                DirectoryInfo directory = new FileInfo(filePath).Directory;

                while (directory != null)
                {
                    // ".git" is a folder in a normal clone and a file in worktrees and submodules
                    string marker = Path.Combine(directory.FullName, Constants.GitMarkerName);

                    if (Directory.Exists(marker) || File.Exists(marker))
                    {
                        return directory.Name;
                    }

                    directory = directory.Parent;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException || ex is UnauthorizedAccessException)
            {
                // An unresolvable path is treated as not being in a repository
            }

            return null;
        }

        #endregion Public Methods
    }
}
