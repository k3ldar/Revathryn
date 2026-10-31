namespace Revathryn.Models.Dashboard
{
    /// <summary>Project-based module visibility including coverage and grouped findings.</summary>
    public sealed class ModuleViewModel
    {
        /// <summary>Display name derived from the project file.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the project file.</summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>Files belonging to the module.</summary>
        public IReadOnlyList<FileSummaryViewModel> Files { get; set; } = [];

        /// <summary>Number of file and review type combinations applicable to the module.</summary>
        public int RequiredReviews { get; set; }

        /// <summary>Number of file and review type combinations with a current completed analysis.</summary>
        public int CompletedReviews { get; set; }

        /// <summary>Number of file and review type combinations still outstanding.</summary>
        public int OutstandingReviews { get; set; }

        /// <summary>Completed reviews as a percentage of required reviews.</summary>
        public int ReviewCoveragePercent { get; set; }

        /// <summary>Findings grouped by severity and review type.</summary>
        public IReadOnlyList<FindingGroupViewModel> FindingGroups { get; set; } = [];
    }
}
