using Revathryn.Contracts.Models;
using Revathryn.Models.IssueValidations;

namespace Revathryn.Models.Api
{
    /// <summary>A single issue related to a file, enriched for API display.</summary>
    public sealed class FileIssueDetails
    {
        /// <summary>The persisted finding.</summary>
        public Finding Finding { get; init; } = new();

        /// <summary>Display name of the review type that produced the finding.</summary>
        public string ReviewTypeName { get; init; } = string.Empty;

        /// <summary>Repository-relative path of the associated file.</summary>
        public string FileRelativePath { get; init; } = string.Empty;

        /// <summary>Non-sensitive summary of the original AI review context, when retained.</summary>
        public FileReviewContextSummary Context { get; init; }

        /// <summary>Latest completed validation, when one exists.</summary>
        public IssueValidationDto LatestValidation { get; init; }
    }
}
