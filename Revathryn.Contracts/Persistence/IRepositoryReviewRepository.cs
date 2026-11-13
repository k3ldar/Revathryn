using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for repository review type links.</summary>
    public interface IRepositoryReviewRepository
    {
        /// <summary>Returns the link between a repository and review type, or null when not linked.</summary>
        Task<RepositoryReview> GetAsync(long repositoryId, long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns all review type links for a repository.</summary>
        Task<IReadOnlyList<RepositoryReview>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Links a review type to a repository and returns the assigned id.</summary>
        Task<long> AddAsync(RepositoryReview link, CancellationToken cancellationToken = default);

        /// <summary>Sets whether a review type is enabled for a repository, creating the link when absent.</summary>
        Task SetEnabledAsync(long repositoryId, long reviewTypeId, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Links every global review type to the repository; returns the number of new links.</summary>
        Task<int> LinkAllReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Links the review type to every repository; returns the number of new links.</summary>
        Task<int> LinkToAllRepositoriesAsync(long reviewTypeId, CancellationToken cancellationToken = default);
    }
}
