using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for review definitions.</summary>
    public interface IReviewTypeRepository
    {
        /// <summary>Returns the review type with the given id, or null when not found.</summary>
        Task<ReviewType> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all review types belonging to a repository.</summary>
        Task<IReadOnlyList<ReviewType>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns the enabled review types belonging to a repository.</summary>
        Task<IReadOnlyList<ReviewType>> GetEnabledByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Inserts the review type and returns its assigned id.</summary>
        Task<long> AddAsync(ReviewType reviewType, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing review type.</summary>
        Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default);

        /// <summary>Deletes the review type with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
