using Revathryn.Contracts.Models;

namespace Revathryn.Models.Issues
{
    /// <summary>Complete JSON export of every issue belonging to a repository.</summary>
    public sealed class IssueExportViewModel
    {
        /// <summary>The repository the issues belong to.</summary>
        public Repository Repository { get; init; } = new();

        /// <summary>UTC timestamp when the export was produced.</summary>
        public DateTime ExportedUtc { get; init; }

        /// <summary>Every issue in the repository, newest first, without filtering or paging.</summary>
        public IReadOnlyList<IssueExportItemViewModel> Issues { get; init; } = [];
    }
}
