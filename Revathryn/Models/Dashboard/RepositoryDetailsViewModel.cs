using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>Repository and module-level lifecycle visibility for the dashboard detail view.</summary>
    public sealed class RepositoryDetailsViewModel
    {
        /// <summary>The registered repository.</summary>
        public Repository Repository { get; set; }

        /// <summary>Number of files currently discovered for the repository.</summary>
        public int FilesDiscovered { get; set; }

        /// <summary>Number of discovered files that have at least one applicable review type.</summary>
        public int FilesRequiringReview { get; set; }

        /// <summary>Number of outstanding file and review type combinations.</summary>
        public int OutstandingReviews { get; set; }

        /// <summary>Number of non-operational findings recorded for the repository.</summary>
        public int FindingsCount { get; set; }

        /// <summary>Localised review status text.</summary>
        public string ReviewStatus { get; set; } = string.Empty;

        /// <summary>Project-based modules with their file coverage and findings.</summary>
        public IReadOnlyList<ModuleViewModel> Modules { get; set; } = new List<ModuleViewModel>();

        /// <summary>Files not associated with any discovered project.</summary>
        public IReadOnlyList<FileSummaryViewModel> UnassignedFiles { get; set; } = new List<FileSummaryViewModel>();
    }
}
