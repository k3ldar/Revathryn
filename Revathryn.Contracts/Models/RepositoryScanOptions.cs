namespace Revathryn.Contracts.Models
{
    /// <summary>Configurable rules applied when scanning a repository folder.</summary>
    public class RepositoryScanOptions
    {
        /// <summary>Directory names that are ignored during enumeration.</summary>
        public IReadOnlyCollection<string> ExcludedDirectoryNames { get; set; }

        /// <summary>
        /// File extensions, without a leading dot, that are retained by a scan.
        /// An empty or null collection retains every file.
        /// </summary>
        public IReadOnlyCollection<string> IncludedExtensions { get; set; }

        /// <summary>Whether .gitignore files should be honoured during discovery.</summary>
        public bool RespectGitIgnore { get; set; }

        /// <summary>Optional glob patterns that discovered files must match to be retained.</summary>
        public IReadOnlyCollection<string> IncludedFilePatterns { get; set; }

        /// <summary>Optional glob patterns that exclude matching discovered files.</summary>
        public IReadOnlyCollection<string> ExcludedFilePatterns { get; set; }
    }
}
