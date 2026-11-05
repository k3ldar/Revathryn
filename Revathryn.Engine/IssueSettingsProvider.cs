using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>
    /// Holds the current duplicate-detection and issue-browsing settings so that
    /// long-lived services observe settings-page changes without restarting.
    /// </summary>
    public sealed class IssueSettingsProvider : IIssueSettingsProvider
    {
        private IssueSimilarityOptions _issueSimilarity = new();
        private IssueViewingOptions _issueViewing = new();

        /// <summary>The currently active duplicate-detection settings.</summary>
        public IssueSimilarityOptions IssueSimilarity => _issueSimilarity;

        /// <summary>The currently active issue-browsing settings.</summary>
        public IssueViewingOptions IssueViewing => _issueViewing;

        /// <summary>Replaces the active settings snapshot with the supplied persisted settings.</summary>
        public void Refresh(AISettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            IssueSimilarityOptions similarity = new()
            {
                Enabled = settings.IssueSimilarityEnabled,
                Threshold = settings.IssueSimilarityThreshold,
                MinimumTokenLength = settings.IssueSimilarityMinimumTokenLength
            };

            int[] pageSizeOptions = [.. IssuePageSizeOptionsFormatter.Parse(settings.IssuePageSizeOptions)];

            if (pageSizeOptions.Length == 0)
            {
                IssueViewingOptions defaults = new();
                pageSizeOptions = defaults.PageSizeOptions;
            }

            int defaultPageSize = settings.IssueDefaultPageSize >= Consts.MinimumIssuePageSize
                ? settings.IssueDefaultPageSize
                : Consts.IssueDefaultPageSize;

            if (!pageSizeOptions.Contains(defaultPageSize))
            {
                pageSizeOptions = [.. pageSizeOptions, defaultPageSize];
            }

            IssueViewingOptions viewing = new()
            {
                DefaultPageSize = defaultPageSize,
                PageSizeOptions = [.. pageSizeOptions.OrderBy(pageSize => pageSize)]
            };

            _issueSimilarity = similarity;
            _issueViewing = viewing;
        }
    }
}
