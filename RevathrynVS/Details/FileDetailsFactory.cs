using System.Globalization;
using System.Windows.Input;

using Newtonsoft.Json.Linq;

namespace RevathrynVS2026.Details
{
    /// <summary>Builds <see cref="FileDetailsViewModel"/> from the endpoint's file details JSON.</summary>
    internal static class FileDetailsFactory
    {
        #region Fields
        private const int OperationalCategory = 1;

        private const string CompletedStatus = "Completed";

        private const string RulePrefix = "rule:";
        #endregion Fields

        #region Public Methods
        /// <summary>Creates the details view model when <paramref name="root"/> matches the details schema.</summary>
        /// <param name="root">The parsed endpoint response.</param>
        /// <param name="navigate">Moves the editor to a line range of the file.</param>
        /// <param name="changeSeverity">Changes a finding's severity.</param>
        /// <param name="changeStatus">Changes a finding's status.</param>
        /// <param name="resubmit">Re-queues a finding's file for review.</param>
        /// <param name="viewOnline">Opens a finding on the website.</param>
        /// <returns>The view model, or <see langword="null"/> when the response is not a file details object.</returns>
        public static FileDetailsViewModel TryCreate(
            JToken root,
            Action<int, int> navigate,
            Action<long, IssueSeverity> changeSeverity,
            Action<long, IssueStatus> changeStatus,
            Action<long> resubmit,
            Action<long> viewOnline)
        {
            if (navigate == null)
            {
                throw new ArgumentNullException(nameof(navigate));
            }

            if (changeSeverity == null)
            {
                throw new ArgumentNullException(nameof(changeSeverity));
            }

            if (changeStatus == null)
            {
                throw new ArgumentNullException(nameof(changeStatus));
            }

            if (resubmit == null)
            {
                throw new ArgumentNullException(nameof(resubmit));
            }

            if (viewOnline == null)
            {
                throw new ArgumentNullException(nameof(viewOnline));
            }

            if (!(root is JObject details) || !(Get(details, "file") is JObject file))
            {
                return null;
            }

            JObject repository = Get(details, "repository") as JObject;
            List<string> notices = [];
            IReadOnlyList<IssueViewModel> issues = CreateIssues(details, navigate, changeSeverity, changeStatus, resubmit, viewOnline, notices);
            IReadOnlyList<ReviewViewModel> reviews = CreateReviews(details, out int completed);
            JObject architecture = Get(details, "architecture") as JObject;

            return new FileDetailsViewModel(
                Text(file, "fileName"),
                BuildLocation(Text(repository, "name"), Text(file, "relativePath")),
                FormatScan(Date(repository, "lastSuccessfulScanDate")),
                Text(architecture, "summary"),
                BuildArchitectureCaption(architecture),
                issues,
                notices,
                string.Format(CultureInfo.CurrentCulture, DetailsMessages.ReviewsSummaryFormat, completed, reviews.Count),
                reviews,
                CreateStrings(details, "systemDependencies"),
                CreateStrings(details, "otherDependencies"));
        }

        #endregion Public Methods

        #region Private Methods
        private static IReadOnlyList<IssueViewModel> CreateIssues(
            JObject details,
            Action<int, int> navigate,
            Action<long, IssueSeverity> changeSeverity,
            Action<long, IssueStatus> changeStatus,
            Action<long> resubmit,
            Action<long> viewOnline,
            List<string> notices)
        {
            List<IssueViewModel> issues = [];

            foreach (JObject item in Objects(details, "issues"))
            {
                if (!(Get(item, "finding") is JObject finding))
                {
                    continue;
                }

                string title = Text(finding, "title");
                string description = Text(finding, "description");
                string reviewType = Text(item, "reviewTypeName");

                // Operational findings (for example a failed review run) are not code issues
                if (Int(finding, "category", 0) == OperationalCategory)
                {
                    notices.Add(string.Format(CultureInfo.CurrentCulture, DetailsMessages.NoticeFormat, title, reviewType, description));
                    continue;
                }

                long findingId = Long(finding, "id", 0);
                int lineStart = Int(finding, "lineStart", 0);
                int lineEnd = Int(finding, "lineEnd", lineStart);
                ICommand goToLine = new RelayCommand(() => navigate(lineStart, Math.Max(lineStart, lineEnd)), lineStart > 0);
                string rule = Text(finding, "findingKey");

                if (rule != null && rule.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    rule = rule.Substring(RulePrefix.Length);
                }

                issues.Add(new IssueViewModel(
                    findingId,
                    title,
                    description,
                    Text(finding, "suggestedFix"),
                    Int(finding, "severity", 0),
                    Int(finding, "status", 0),
                    reviewType,
                    rule,
                    lineStart,
                    lineEnd,
                    goToLine,
                    changeSeverity,
                    changeStatus,
                    resubmit,
                    viewOnline));
            }

            return [.. issues
          .OrderByDescending(issue => (int)issue.Severity)
          .ThenBy(issue => issue.LineStart > 0 ? issue.LineStart : int.MaxValue)];
        }

        private static IReadOnlyList<ReviewViewModel> CreateReviews(JObject details, out int completed)
        {
            List<ReviewViewModel> reviews = [];
            completed = 0;

            foreach (JObject review in Objects(details, "reviews"))
            {
                string status = Text(review, "status");
                DateTime? analysed = Date(review, "lastAnalysedUtc");

                if (string.Equals(status, CompletedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    completed++;
                }

                reviews.Add(new ReviewViewModel(
                    Text(review, "reviewTypeName"),
                    status,
                    analysed.HasValue ? analysed.Value.ToString("g", CultureInfo.CurrentCulture) : null));
            }

            return reviews;
        }

        private static IReadOnlyList<string> CreateStrings(JObject details, string name)
        {
            return [.. Strings(details, name)];
        }

        private static string BuildLocation(string repositoryName, string relativePath)
        {
            if (string.IsNullOrEmpty(repositoryName))
            {
                return relativePath;
            }

            return string.IsNullOrEmpty(relativePath) ? repositoryName : $"{repositoryName}: {relativePath}";
        }

        private static string FormatScan(DateTime? scanned)
        {
            return scanned.HasValue
                ? string.Format(CultureInfo.CurrentCulture, DetailsMessages.LastScanFormat, scanned.Value.ToString("g", CultureInfo.CurrentCulture))
                : null;
        }

        private static string BuildArchitectureCaption(JObject architecture)
        {
            string engine = Text(architecture, "engineName");
            DateTime? created = Date(architecture, "createdUtc");

            if (string.IsNullOrEmpty(engine) || !created.HasValue)
            {
                return null;
            }

            return string.Format(CultureInfo.CurrentCulture, DetailsMessages.ArchitectureCaptionFormat, engine, created.Value.ToString("g", CultureInfo.CurrentCulture));
        }

        private static JToken Get(JObject owner, string name)
        {
            return owner?.GetValue(name, StringComparison.OrdinalIgnoreCase);
        }

        private static string Text(JObject owner, string name)
        {
            return Get(owner, name) is JValue value && value.Value != null
                ? Convert.ToString(value.Value, CultureInfo.InvariantCulture)
                : null;
        }

        private static int Int(JObject owner, string name, int fallback)
        {
            return int.TryParse(Text(owner, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }

        private static long Long(JObject owner, string name, long fallback)
        {
            return long.TryParse(Text(owner, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : fallback;
        }

        private static DateTime? Date(JObject owner, string name)
        {
            if (!(Get(owner, name) is JValue value) || value.Value == null)
            {
                return null;
            }

            DateTime parsed;

            if (value.Value is DateTime date)
            {
                parsed = date;
            }
            else if (!DateTime.TryParse(Convert.ToString(value.Value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                return null;
            }

            // Unset dates arrive as 0001-01-01
            if (parsed.Year <= 1)
            {
                return null;
            }

            return parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed;
        }

        private static IEnumerable<JObject> Objects(JObject owner, string name)
        {
            return Get(owner, name) is JArray array ? array.OfType<JObject>() : [];
        }

        private static IEnumerable<string> Strings(JObject owner, string name)
        {
            return Get(owner, name) is JArray array
                ? array.OfType<JValue>().Select(value => Convert.ToString(value.Value, CultureInfo.InvariantCulture)).Where(text => !string.IsNullOrEmpty(text))
                : [];
        }

        #endregion Private Methods
    }
}
