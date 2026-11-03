using System.ComponentModel.DataAnnotations;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>Form model for creating and editing a review type.</summary>
    public sealed class ReviewTypeEditViewModel
    {
        /// <summary>Review type identifier; zero when creating.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository identifier.</summary>
        [Range(Consts.MinimumRepositoryId, int.MaxValue, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.RepositoryRequiredMessage))]
        public long RepositoryId { get; set; }

        /// <summary>Display name of the review type.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.NameRequiredMessage))]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of what the review targets.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Comma-separated list of file extensions this review applies to.</summary>
        public string FileExtensions { get; set; } = string.Empty;

        /// <summary>The review-specific prompt combined with the master prompt at execution time.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Default queue priority; higher values are processed first.</summary>
        public int DefaultPriority { get; set; } = Consts.DefaultReviewTypePriority;

        /// <summary>Whether the review type is active.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Repositories available for selection; not persisted.</summary>
        public IReadOnlyList<Repository> Repositories { get; set; } = [];

        /// <summary>Creates a form model from a persisted review type.</summary>
        public static ReviewTypeEditViewModel FromReviewType(ReviewType reviewType)
        {
            return new ReviewTypeEditViewModel
            {
                Id = reviewType.Id,
                RepositoryId = reviewType.RepositoryId,
                Name = reviewType.Name,
                Description = reviewType.Description,
                FileExtensions = reviewType.FileExtensions,
                Prompt = reviewType.Prompt,
                DefaultPriority = reviewType.DefaultPriority,
                Enabled = reviewType.Enabled
            };
        }

        /// <summary>Converts the form model to a persisted review type.</summary>
        public ReviewType ToReviewType()
        {
            return new ReviewType
            {
                Id = Id,
                RepositoryId = RepositoryId,
                Key = string.Empty,
                Name = Name?.Trim() ?? string.Empty,
                Description = Description?.Trim() ?? string.Empty,
                FileExtensions = FileExtensions?.Trim() ?? string.Empty,
                Prompt = Prompt?.Trim() ?? string.Empty,
                DefaultPriority = DefaultPriority,
                Enabled = Enabled
            };
        }
    }
}
