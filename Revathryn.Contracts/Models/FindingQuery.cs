using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>Filtering, sorting and paging options for querying findings.</summary>
    public sealed class FindingQuery
    {
        /// <summary>Restrict results to a single repository, or null for all repositories.</summary>
        public long? RepositoryId { get; set; }

        /// <summary>Restrict results to a single severity, or null for all severities.</summary>
        public FindingSeverity? Severity { get; set; }

        /// <summary>Restrict results to a single review type, or null for all review types.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Restrict results to a single category, or null for all categories.</summary>
        public FindingCategory? Category { get; set; }

        /// <summary>Restrict results to a single status, or null for all statuses.</summary>
        public FindingStatus? Status { get; set; }

        /// <summary>Free-text search applied to title and description.</summary>
        public string SearchText { get; set; } = string.Empty;

        /// <summary>Field used to order results.</summary>
        public FindingSortField SortField { get; set; } = FindingSortField.CreatedDate;

        /// <summary>Whether results are ordered descending.</summary>
        public bool SortDescending { get; set; } = true;

        /// <summary>Zero-based page index.</summary>
        public int PageIndex { get; set; }

        /// <summary>Maximum number of items per page.</summary>
        public int PageSize { get; set; } = Consts.DefaultIssuePageSize;
    }
}
