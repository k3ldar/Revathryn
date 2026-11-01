namespace Revathryn.Contracts.Services
{
    /// <summary>Seeds built-in review definitions for a repository.</summary>
    public interface IReviewTypeSeeder
    {
        /// <summary>
        /// Inserts any built-in review definitions that are missing for the repository.
        /// Returns the number of review definitions that were inserted.
        /// </summary>
        /// <param name="repositoryId">The repository to seed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<int> SeedAsync(long repositoryId, CancellationToken cancellationToken = default);
    }
}
