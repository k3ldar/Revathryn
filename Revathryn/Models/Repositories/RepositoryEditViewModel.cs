using System.ComponentModel.DataAnnotations;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Models.Repositories
{
    /// <summary>Form model for creating and editing a repository.</summary>
    public sealed class RepositoryEditViewModel
    {
        /// <summary>Repository identifier; zero when creating.</summary>
        public long Id { get; set; }

        /// <summary>Human readable repository name.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.NameRequiredMessage))]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of the repository.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Local folder path containing the repository source.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.LocalPathRequiredMessage))]
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>Whether the repository participates in processing.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether file changes should automatically enqueue reviews.</summary>
        public bool AutomaticReviewEnabled { get; set; } = true;

        /// <summary>Maximum number of context requests permitted per review.</summary>
        [Range(1, int.MaxValue, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.MaxContextRequestsRangeMessage))]
        public int MaxContextRequests { get; set; } = DefaultMaxContextRequests;

        /// <summary>Minimum severity at which a newly created finding is automatically re-reviewed.</summary>
        public FindingSeverity AutoReviewMinimumSeverity { get; set; } = (FindingSeverity)DefaultAutoReviewMinimumSeverity;

        /// <summary>Required review AI confidence, as a percentage between 0 and 100, before its recommendation is applied.</summary>
        [Range(MinimumAutoReviewConfidenceThreshold, MaximumAutoReviewConfidenceThreshold, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.AutoReviewConfidenceThresholdRangeMessage))]
        public int AutoReviewConfidenceThreshold { get; set; } = DefaultAutoReviewConfidenceThreshold;

        /// <summary>Identifier of the AI engine used for this repository's reviews, or null to use the system default.</summary>
        public long? AIEngineId { get; set; }

        /// <summary>AI engines available for selection, including disabled engines.</summary>
        public IReadOnlyList<AIEngine> AvailableEngines { get; set; } = [];

        /// <summary>Creates a form model from a persisted repository.</summary>
        public static RepositoryEditViewModel FromRepository(Repository repository)
        {
            return new RepositoryEditViewModel
            {
                Id = repository.Id,
                Name = repository.Name,
                Description = repository.Description,
                LocalPath = repository.LocalPath,
                Enabled = repository.Enabled,
                AutomaticReviewEnabled = repository.AutomaticReviewEnabled,
                MaxContextRequests = repository.MaxContextRequests,
                AutoReviewMinimumSeverity = repository.AutoReviewMinimumSeverity,
                AutoReviewConfidenceThreshold = repository.AutoReviewConfidenceThreshold,
                AIEngineId = repository.AIEngineId
            };
        }

        /// <summary>Converts the form model to a persisted repository.</summary>
        public Repository ToRepository()
        {
            return new Repository
            {
                Id = Id,
                Name = Name?.Trim() ?? string.Empty,
                Description = Description?.Trim() ?? string.Empty,
                LocalPath = LocalPath?.Trim() ?? string.Empty,
                Enabled = Enabled,
                AutomaticReviewEnabled = AutomaticReviewEnabled,
                MaxContextRequests = MaxContextRequests,
                AutoReviewMinimumSeverity = AutoReviewMinimumSeverity,
                AutoReviewConfidenceThreshold = AutoReviewConfidenceThreshold,
                AIEngineId = AIEngineId
            };
        }
    }
}
