using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>File-level review coverage summary for lists.</summary>
    public sealed class FileSummaryViewModel
    {
        /// <summary>The discovered file.</summary>
        public RepoFile File { get; set; }

        /// <summary>Number of applicable review types with a current completed analysis.</summary>
        public int CompletedReviews { get; set; }

        /// <summary>Number of applicable review types still outstanding.</summary>
        public int OutstandingReviews { get; set; }

        /// <summary>Number of non-operational findings recorded for the file.</summary>
        public int FindingsCount { get; set; }
    }
}
