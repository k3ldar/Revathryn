using Revathryn.Contracts.Models;
using Revathryn.Models.IssueValidations;

namespace Revathryn.Models.Issues
{
    /// <summary>A single issue within a JSON export, including its complete detail.</summary>
    public sealed class IssueExportItemViewModel
    {
        /// <summary>The persisted finding.</summary>
        public Finding Finding { get; init; } = new();

        /// <summary>Display name of the review type that produced the finding.</summary>
        public string ReviewTypeName { get; init; } = string.Empty;

        /// <summary>Repository-relative path of the associated file, when available.</summary>
        public string FileRelativePath { get; init; } = string.Empty;

        /// <summary>Original AI review context, including the reviewed content and instructions.</summary>
        public AIReviewContext Context { get; init; }

        /// <summary>Full detail of the latest completed validation, when one exists.</summary>
        public IssueValidationDto LatestValidation { get; init; }
    }
}
