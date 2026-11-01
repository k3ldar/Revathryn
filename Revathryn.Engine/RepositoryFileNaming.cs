using System.Text;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Derives filesystem-safe names for per-repository analysis artefacts.</summary>
    public static class RepositoryFileNaming
    {
        /// <summary>
        /// Converts a repository display name into a safe file and directory name.
        /// Whitespace and characters that are invalid in file names are replaced
        /// with a single hyphen.
        /// </summary>
        /// <param name="repositoryName">The human-readable repository name.</param>
        /// <returns>A filesystem-safe name, never empty.</returns>
        public static string GetSafeName(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                return DefaultRepositoryFolderName;
            }

            string trimmed = repositoryName.Trim();
            HashSet<char> invalidCharacters = new(Path.GetInvalidFileNameChars());
            StringBuilder builder = new(trimmed.Length);
            bool previousWasSeparator = false;

            foreach (char character in trimmed)
            {
                if (invalidCharacters.Contains(character) || char.IsWhiteSpace(character))
                {
                    if (!previousWasSeparator)
                    {
                        builder.Append(CharHyphen);
                        previousWasSeparator = true;
                    }

                    continue;
                }

                builder.Append(character);
                previousWasSeparator = false;
            }

            string result = builder.ToString().Trim(CharHyphen);
            return result.Length == 0 ? DefaultRepositoryFolderName : result;
        }
    }
}
