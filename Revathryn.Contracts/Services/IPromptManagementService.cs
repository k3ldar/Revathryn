using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Manages prompt versions and their lifecycle: drafts, committing, independent activation and rollback.
    /// Activation always requires an explicit caller action and is recorded as a new historical state.
    /// </summary>
    public interface IPromptManagementService
    {
        /// <summary>Returns the prompt management overview rows.</summary>
        Task<IReadOnlyList<PromptOverviewItem>> GetOverviewAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns a prompt identity by id, or null when not found.</summary>
        Task<Prompt> GetPromptAsync(long promptId, CancellationToken cancellationToken = default);

        /// <summary>Returns all versions for a prompt identity, newest first.</summary>
        Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(long promptId, CancellationToken cancellationToken = default);

        /// <summary>Returns a prompt version by id, or null when not found.</summary>
        Task<PromptVersion> GetVersionAsync(long versionId, CancellationToken cancellationToken = default);

        /// <summary>Creates a new editable draft version for a prompt identity.</summary>
        Task<PromptVersion> CreateDraftAsync(long promptId, string promptText, string description, PromptOrigin origin, long? sourceOptimisationId, string createdBy, CancellationToken cancellationToken = default);

        /// <summary>Updates the text and description of an existing draft.</summary>
        Task<PromptVersion> UpdateDraftAsync(long versionId, string promptText, string description, CancellationToken cancellationToken = default);

        /// <summary>Commits a draft as an immutable historical version without activating it.</summary>
        Task<PromptVersion> CommitDraftAsync(long versionId, CancellationToken cancellationToken = default);

        /// <summary>Explicitly activates a version and deactivates all others of the same prompt identity.</summary>
        Task<PromptVersion> ActivateAsync(long versionId, string activatedBy, CancellationToken cancellationToken = default);

        /// <summary>Returns the system prompt recommendation produced by an optimisation, or null.</summary>
        Task<PromptRecommendation> GetSystemRecommendationAsync(long optimisationId, CancellationToken cancellationToken = default);

        /// <summary>Returns the review prompt recommendation produced by an optimisation, or null.</summary>
        Task<PromptRecommendation> GetReviewRecommendationAsync(long optimisationId, CancellationToken cancellationToken = default);
    }
}
