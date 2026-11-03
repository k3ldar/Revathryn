using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for logical prompt identities (system prompt or review-specific prompts).</summary>
    public interface IPromptRepository
    {
        /// <summary>Returns a prompt identity by id, or null when not found.</summary>
        Task<Prompt> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns the single system prompt identity, or null when not seeded.</summary>
        Task<Prompt> GetSystemAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the prompt identity for a review type, or null when not seeded.</summary>
        Task<Prompt> GetByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns every prompt identity, ordered by type then review type id.</summary>
        Task<IReadOnlyList<Prompt>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts a prompt identity and returns its assigned id.</summary>
        Task<long> AddAsync(Prompt prompt, CancellationToken cancellationToken = default);

        /// <summary>Updates the last-modified timestamp of an existing prompt identity.</summary>
        Task TouchAsync(long id, DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
