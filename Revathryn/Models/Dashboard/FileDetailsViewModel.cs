using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>File-level lifecycle visibility including outcomes and findings.</summary>
    public sealed class FileDetailsViewModel
    {
        /// <summary>The owning repository.</summary>
        public Repository Repository { get; set; }

        /// <summary>The discovered file.</summary>
        public RepoFile File { get; set; }

        /// <summary>Review types applicable to the file.</summary>
        public IReadOnlyList<ReviewType> ApplicableReviewTypes { get; set; } = new List<ReviewType>();

        /// <summary>Current completed review outcomes for the file.</summary>
        public IReadOnlyList<ReviewOutcomeViewModel> CompletedReviews { get; set; } = new List<ReviewOutcomeViewModel>();

        /// <summary>Applicable review types that do not yet have a current completed outcome.</summary>
        public IReadOnlyList<ReviewType> OutstandingReviewTypes { get; set; } = new List<ReviewType>();

        /// <summary>Findings recorded against the file.</summary>
        public IReadOnlyList<FindingViewModel> Findings { get; set; } = new List<FindingViewModel>();
    }
}
