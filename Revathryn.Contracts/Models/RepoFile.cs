namespace Revathryn.Contracts.Models
{
    /// <summary>A file discovered within a repository.</summary>
    public class RepoFile
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Path relative to the repository root; the repository-scoped identity.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>File name including extension.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>File extension without the leading dot, lower-cased.</summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>UTC last write time of the file on disk.</summary>
        public DateTime LastModifiedDate { get; set; }

        /// <summary>SHA256 hash of the file contents.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>File size in bytes.</summary>
        public long FileSize { get; set; }

        /// <summary>Whether the file is excluded from review.</summary>
        public bool IsExcluded { get; set; }

        /// <summary>Whether the file was previously discovered but is no longer present in the repository.</summary>
        public bool IsRemoved { get; set; }

        /// <summary>Repository-relative path of the containing project file, where identifiable.</summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the file was last discovered by a scan.</summary>
        public DateTime LastDiscoveredDate { get; set; }
    }
}
