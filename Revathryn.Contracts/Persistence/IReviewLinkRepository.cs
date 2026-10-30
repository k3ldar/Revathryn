using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for review-to-file links.</summary>
    public interface IReviewLinkRepository
    {
        /// <summary>Returns the link with the given id, or null when not found.</summary>
        Task<ReviewLink> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns the link between a file and a review type, or null when not found.</summary>
        Task<ReviewLink> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns all links for a file.</summary>
        Task<IReadOnlyList<ReviewLink>> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Returns all links for a review type.</summary>
        Task<IReadOnlyList<ReviewLink>> GetByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Inserts or updates the link and returns its id.</summary>
        Task<long> UpsertAsync(ReviewLink link, CancellationToken cancellationToken = default);

        /// <summary>Deletes the link with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Deletes all links for a file.</summary>
        Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default);
    }
}
