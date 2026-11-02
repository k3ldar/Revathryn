using Revathryn.Contracts.Models;

namespace Revathryn.Models.Issues
{
    /// <summary>A single row in the issue listing.</summary>
    public sealed class IssueListItemViewModel
    {
        /// <summary>The persisted finding.</summary>
        public Finding Finding { get; set; } = new();

        /// <summary>Display name of the review type that produced the finding.</summary>
        public string ReviewTypeName { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the associated file, when available.</summary>
        public string FileRelativePath { get; set; } = string.Empty;
    }
}
