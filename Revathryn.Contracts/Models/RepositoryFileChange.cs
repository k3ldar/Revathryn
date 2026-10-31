using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A single file-level change detected during repository reconciliation.</summary>
    public sealed class RepositoryFileChange
    {
        /// <summary>The repository the change belongs to.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Identifier of the repository file record.</summary>
        public long FileId { get; set; }

        /// <summary>Repository-relative path of the file after the change.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Previous repository-relative path when the change is a rename or move.</summary>
        public string PreviousRelativePath { get; set; } = string.Empty;

        /// <summary>Category of the change.</summary>
        public RepositoryChangeType ChangeType { get; set; }

        /// <summary>Content fingerprint associated with the change.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>File size in bytes associated with the change.</summary>
        public long FileSize { get; set; }

        /// <summary>UTC last write time associated with the change.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
