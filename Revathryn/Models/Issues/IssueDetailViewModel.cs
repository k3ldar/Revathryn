using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Models.IssueValidations;

namespace Revathryn.Models.Issues
{
    /// <summary>Detail view for a single issue, including the original AI review context.</summary>
    public sealed class IssueDetailViewModel
    {
        /// <summary>The persisted finding.</summary>
        public Finding Finding { get; set; } = new();

        /// <summary>The owning repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>Display name of the review type that produced the finding.</summary>
        public string ReviewTypeName { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the associated file, when available.</summary>
        public string FileRelativePath { get; set; } = string.Empty;

        /// <summary>Original AI context for this finding, or null when not retained.</summary>
        public AIReviewContext Context { get; set; }

        /// <summary>Id of the preceding finding in the current listing ordering, or null.</summary>
        public long? PreviousFindingId { get; set; }

        /// <summary>Id of the following finding in the current listing ordering, or null.</summary>
        public long? NextFindingId { get; set; }

        /// <summary>Listing filters, ordering and paging to preserve when navigating back.</summary>
        public IssueListQueryState ListState { get; set; } = new();

        /// <summary>Independent AI validation section: availability, current result and history.</summary>
        public IssueValidationPanelViewModel Validation { get; set; }
    }


    public sealed class IssueListQueryState
    {
        /// <summary>Repository being browsed.</summary>
        public long RepositoryId { get; set; }

        /// <summary>One-based page index.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Number of issues per page.</summary>
        public int PageSize { get; set; }

        /// <summary>Severity filter, or null for all severities.</summary>
        public FindingSeverity? Severity { get; set; }

        /// <summary>Review type filter, or null for all review types.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Search text.</summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>Sort field.</summary>
        public FindingSortField SortField { get; set; }

        /// <summary>Whether the sort is descending.</summary>
        public bool SortDescending { get; set; } = true;
    }
}
