using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Issues
{
    /// <summary>Paginated issue listing for a single repository.</summary>
    public sealed class IssueListViewModel
    {
        /// <summary>The selected repository.</summary>
        public Repository Repository { get; set; } = new();

        /// <summary>Issues in the current page.</summary>
        public IReadOnlyList<IssueListItemViewModel> Items { get; set; } = [];

        /// <summary>Review types available to filter by.</summary>
        public IReadOnlyList<ReviewType> ReviewTypes { get; set; } = [];

        /// <summary>Total number of issues matching the current filters.</summary>
        public int TotalCount { get; set; }

        /// <summary>One-based current page index.</summary>
        public int PageIndex { get; set; }

        /// <summary>Number of issues per page.</summary>
        public int PageSize { get; set; }

        /// <summary>Total number of pages; zero when there are no results.</summary>
        public int TotalPages { get; set; }

        /// <summary>Page sizes offered to the user.</summary>
        public int[] PageSizeOptions { get; set; } = [];

        /// <summary>Active severity filter, or null for all severities.</summary>
        public FindingSeverity? Severity { get; set; }

        /// <summary>Active review type filter, or null for all review types.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Active search text.</summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>Active sort field.</summary>
        public FindingSortField SortField { get; set; }

        /// <summary>Whether the active sort is descending.</summary>
        public bool SortDescending { get; set; }
    }
}
