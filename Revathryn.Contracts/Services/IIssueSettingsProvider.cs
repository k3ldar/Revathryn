using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Provides live duplicate-detection and issue-browsing settings to runtime services.</summary>
    public interface IIssueSettingsProvider
    {
        /// <summary>The currently active duplicate-detection settings.</summary>
        IssueSimilarityOptions IssueSimilarity { get; }

        /// <summary>The currently active issue-browsing settings.</summary>
        IssueViewingOptions IssueViewing { get; }

        /// <summary>Replaces the active settings snapshot with the supplied persisted settings.</summary>
        void Refresh(AISettings settings);
    }
}
