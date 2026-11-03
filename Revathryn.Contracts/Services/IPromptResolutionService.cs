using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Resolves the active system and review-specific prompts for detection, independently of one another,
    /// and seeds initial prompt versions from existing configuration when none exist.
    /// </summary>
    public interface IPromptResolutionService
    {
        /// <summary>Resolves the active system and review-specific prompts for a review type.</summary>
        Task<ResolvedPrompts> ResolveForDetectionAsync(long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns the active system prompt version, or null when none is active.</summary>
        Task<PromptVersion> GetActiveSystemVersionAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the active review-specific prompt version, or null when none is active.</summary>
        Task<PromptVersion> GetActiveReviewVersionAsync(long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds a system prompt identity and initial active version from the configured master prompt,
        /// and a review-specific prompt identity and initial active version from each configured review type.
        /// Existing prompt identities are left untouched. This is idempotent.
        /// </summary>
        Task SeedFromConfigurationAsync(CancellationToken cancellationToken = default);
    }
}
