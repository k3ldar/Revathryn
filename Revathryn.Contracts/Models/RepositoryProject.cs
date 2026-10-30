using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A project or solution structure discovered within a repository.</summary>
    public class RepositoryProject
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Display name derived from the project or solution file.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether this entry represents a solution or a project.</summary>
        public ProjectKind Kind { get; set; }

        /// <summary>Repository-relative path of the project or solution file.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Repository-relative directory containing the project or solution file.</summary>
        public string DirectoryPath { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the containing solution; discovery-only hint.</summary>
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>Id of the containing solution, resolved during reconciliation.</summary>
        public long? SolutionId { get; set; }
    }
}
