using System.Globalization;

namespace RevathrynVS2026.Details
{
    /// <summary>Everything the pane shows for one file when the endpoint returns the details schema.</summary>
    internal sealed class FileDetailsViewModel
    {
        #region Constructors
        public FileDetailsViewModel(
            string fileName,
            string locationText,
            string lastScanText,
            string architectureSummary,
            string architectureCaption,
            IReadOnlyList<IssueViewModel> issues,
            IReadOnlyList<string> notices,
            string reviewsSummary,
            IReadOnlyList<ReviewViewModel> reviews,
            IReadOnlyList<string> systemDependencies,
            IReadOnlyList<string> otherDependencies)
        {
            FileName = fileName;
            LocationText = locationText;
            LastScanText = lastScanText;
            ArchitectureSummary = architectureSummary;
            ArchitectureCaption = architectureCaption;
            Issues = issues;
            Notices = notices;
            ReviewsSummary = reviewsSummary;
            Reviews = reviews;
            SystemDependencies = systemDependencies;
            OtherDependencies = otherDependencies;
            IssuesHeader = string.Format(CultureInfo.CurrentCulture, DetailsMessages.IssuesHeaderFormat, issues.Count);
            SystemDependenciesHeader = string.Format(CultureInfo.CurrentCulture, DetailsMessages.SystemDependenciesHeaderFormat, systemDependencies.Count);
            OtherDependenciesHeader = string.Format(CultureInfo.CurrentCulture, DetailsMessages.OtherDependenciesHeaderFormat, otherDependencies.Count);
        }

        #endregion Constructors

        #region Properties
        public string FileName { get; }

        public string LocationText { get; }

        public string LastScanText { get; }

        public string ArchitectureSummary { get; }

        public string ArchitectureCaption { get; }

        public bool HasArchitecture => !string.IsNullOrWhiteSpace(ArchitectureSummary);

        public IReadOnlyList<IssueViewModel> Issues { get; }

        public string IssuesHeader { get; }

        public IReadOnlyList<string> Notices { get; }

        public bool HasNotices => Notices.Count > 0;

        public string ReviewsSummary { get; }

        public IReadOnlyList<ReviewViewModel> Reviews { get; }

        public bool HasReviews => Reviews.Count > 0;

        public IReadOnlyList<string> SystemDependencies { get; }

        public string SystemDependenciesHeader { get; }

        public bool HasSystemDependencies => SystemDependencies.Count > 0;

        public IReadOnlyList<string> OtherDependencies { get; }

        public string OtherDependenciesHeader { get; }

        public bool HasOtherDependencies => OtherDependencies.Count > 0;
        #endregion Properties
    }
}
