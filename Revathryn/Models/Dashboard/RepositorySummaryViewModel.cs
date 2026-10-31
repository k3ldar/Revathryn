using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>Repository-level review lifecycle summary shown on the dashboard.</summary>
    public sealed class RepositorySummaryViewModel
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

        /// <summary>Number of operational failure findings recorded for the repository.</summary>
        public int OperationalFailuresCount { get; set; }

        /// <summary>Localised review status text.</summary>
        public string ReviewStatus { get; set; } = string.Empty;
    }
}
