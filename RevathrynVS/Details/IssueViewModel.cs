using System.Globalization;
using System.Windows.Input;

namespace RevathrynVS2026.Details
{
    /// <summary>A code finding for the active file.</summary>
    internal sealed class IssueViewModel
    {
        #region Fields
        private readonly Action<long, IssueSeverity> changeSeverity;

        private readonly Action<long, IssueStatus> changeStatus;
        #endregion Fields

        #region Constructors
        public IssueViewModel(
            long findingId,
            string title,
            string description,
            string suggestedFix,
            int severity,
            int status,
            string reviewType,
            string rule,
            int lineStart,
            int lineEnd,
            ICommand goToLineCommand,
            Action<long, IssueSeverity> changeSeverity,
            Action<long, IssueStatus> changeStatus,
            Action<long> resubmit,
            Action<long> viewOnline)
        {
            FindingId = findingId;
            Title = title;
            Description = description;
            SuggestedFix = suggestedFix;
            Severity = (IssueSeverity)severity;
            Status = (IssueStatus)status;
            LineStart = lineStart;
            GoToLineCommand = goToLineCommand;
            this.changeSeverity = changeSeverity;
            this.changeStatus = changeStatus;
            ResubmitCommand = new RelayCommand(() => resubmit(findingId), true);
            ViewOnlineCommand = new RelayCommand(() => viewOnline(findingId), true);
            SeverityOptions = BuildSeverityOptions();
            StatusOptions = BuildStatusOptions();
            SeverityText = Enum.IsDefined(typeof(IssueSeverity), Severity)
                ? Severity.ToString()
                : string.Format(CultureInfo.CurrentCulture, DetailsMessages.UnknownSeverityFormat, severity);
            StatusText = GetStatusText(Status, status);
            MetaText = BuildMetaText(lineStart, lineEnd, reviewType, rule, StatusText);
        }

        #endregion Constructors

        #region Properties
        /// <summary>Gets the finding identifier used by the write endpoints.</summary>
        public long FindingId { get; }

        public string Title { get; }

        public string Description { get; }

        public string SuggestedFix { get; }

        public bool HasSuggestedFix => !string.IsNullOrWhiteSpace(SuggestedFix);

        public IssueSeverity Severity { get; }

        public string SeverityText { get; }

        public IssueStatus Status { get; }

        public string StatusText { get; }

        /// <summary>Gets the severity choices offered to the user.</summary>
        public IReadOnlyList<IssueOption> SeverityOptions { get; }

        /// <summary>Gets the status choices offered to the user.</summary>
        public IReadOnlyList<IssueOption> StatusOptions { get; }

        /// <summary>Gets or sets the severity currently selected in the UI.</summary>
        public IssueSeverity SelectedSeverity
        {
            get => Severity;
            set
            {
                if (value != Severity)
                {
                    changeSeverity(FindingId, value);
                }
            }
        }

        /// <summary>Gets or sets the status currently selected in the UI.</summary>
        public IssueStatus SelectedStatus
        {
            get => Status;
            set
            {
                if (value != Status)
                {
                    changeStatus(FindingId, value);
                }
            }
        }

        /// <summary>Gets a value indicating whether the finding no longer needs attention.</summary>
        public bool IsClosed => Status == IssueStatus.Resolved || Status == IssueStatus.FalsePositive;

        public int LineStart { get; }

        /// <summary>Gets the line, rule, review type and status on one line.</summary>
        public string MetaText { get; }

        public ICommand GoToLineCommand { get; }

        /// <summary>Gets the command that re-queues the finding's file for review.</summary>
        public ICommand ResubmitCommand { get; }

        /// <summary>Gets the command that opens the finding on the website.</summary>
        public ICommand ViewOnlineCommand { get; }
        #endregion Properties

        #region Private Methods
        private static IReadOnlyList<IssueOption> BuildSeverityOptions()
        {
            return
            [
                new IssueOption(IssueSeverity.Info.ToString(), IssueSeverity.Info),
                new IssueOption(IssueSeverity.Low.ToString(), IssueSeverity.Low),
                new IssueOption(IssueSeverity.Medium.ToString(), IssueSeverity.Medium),
                new IssueOption(IssueSeverity.High.ToString(), IssueSeverity.High),
                new IssueOption(IssueSeverity.Critical.ToString(), IssueSeverity.Critical)
            ];
        }

        private static IReadOnlyList<IssueOption> BuildStatusOptions()
        {
            return
            [
                new IssueOption(GetStatusText(IssueStatus.New, (int)IssueStatus.New), IssueStatus.New),
                new IssueOption(GetStatusText(IssueStatus.Reviewed, (int)IssueStatus.Reviewed), IssueStatus.Reviewed),
                new IssueOption(GetStatusText(IssueStatus.FalsePositive, (int)IssueStatus.FalsePositive), IssueStatus.FalsePositive),
                new IssueOption(GetStatusText(IssueStatus.Resolved, (int)IssueStatus.Resolved), IssueStatus.Resolved),
                new IssueOption(GetStatusText(IssueStatus.Deferred, (int)IssueStatus.Deferred), IssueStatus.Deferred)
            ];
        }

        private static string GetStatusText(IssueStatus status, int rawStatus)
        {
            switch (status)
            {
                case IssueStatus.FalsePositive:
                    return DetailsMessages.StatusFalsePositive;
                case IssueStatus.New:
                case IssueStatus.Reviewed:
                case IssueStatus.Resolved:
                case IssueStatus.Deferred:
                    return status.ToString();
                default:
                    return string.Format(CultureInfo.CurrentCulture, DetailsMessages.UnknownStatusFormat, rawStatus);
            }
        }

        private static string BuildMetaText(int lineStart, int lineEnd, string reviewType, string rule, string statusText)
        {
            List<string> parts = [];

            if (lineStart > 0)
            {
                parts.Add(lineEnd > lineStart
                    ? string.Format(CultureInfo.CurrentCulture, DetailsMessages.LineRangeFormat, lineStart, lineEnd)
                    : string.Format(CultureInfo.CurrentCulture, DetailsMessages.LineFormat, lineStart));
            }

            if (!string.IsNullOrWhiteSpace(reviewType))
            {
                parts.Add(reviewType);
            }

            if (!string.IsNullOrWhiteSpace(rule))
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, DetailsMessages.RuleFormat, rule));
            }

            parts.Add(statusText);
            return string.Join(DetailsMessages.MetaSeparator, parts);
        }

        #endregion Private Methods
    }
}
