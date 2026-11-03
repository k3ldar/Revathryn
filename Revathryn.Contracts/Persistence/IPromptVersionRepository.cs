using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>
    /// Data access for immutable prompt versions. Existing versions are never overwritten; editing creates
    /// a new version, except for drafts which may be edited in place until committed or activated.
    /// </summary>
    public interface IPromptVersionRepository
    {
        /// <summary>Returns a prompt version by id, or null when not found.</summary>
        Task<PromptVersion> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all versions for a prompt identity, newest first.</summary>
        Task<IReadOnlyList<PromptVersion>> GetByPromptAsync(long promptId, CancellationToken cancellationToken = default);

        /// <summary>Returns the active version for a prompt identity, or null when none is active.</summary>
        Task<PromptVersion> GetActiveAsync(long promptId, CancellationToken cancellationToken = default);

        /// <summary>Returns the active system prompt version, or null when none is active.</summary>
        Task<PromptVersion> GetActiveSystemAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the active review-specific prompt version, or null when none is active.</summary>
        Task<PromptVersion> GetActiveByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns the highest version number currently assigned to a prompt identity, or 0.</summary>
        Task<int> GetMaxVersionNumberAsync(long promptId, CancellationToken cancellationToken = default);

        /// <summary>Inserts a prompt version and returns its assigned id.</summary>
        Task<long> AddAsync(PromptVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the text and description of an existing draft. Active and historical versions are immutable.
        /// Returns false when the version is not an editable draft.
        /// </summary>
        Task<bool> UpdateDraftAsync(long versionId, string promptText, string description, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a draft as an immutable historical version without activating it. Returns the committed version.
        /// </summary>
        Task<PromptVersion> CommitDraftAsync(long versionId, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a version, deactivating every other version of the same prompt identity. The version is
        /// committed if it was a draft. The deactivated versions remain historically accessible.
        /// </summary>
        Task<PromptVersion> ActivateAsync(long versionId, string activatedBy, DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
