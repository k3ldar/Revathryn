namespace Revathryn.Contracts.Services
{
    /// <summary>Seeds built-in review definitions and links them to repositories.</summary>
    public interface IReviewTypeSeeder
    {
        /// <summary>
        /// Inserts any built-in review definitions that are missing globally.
        /// Returns the number of review definitions that were inserted.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<int> SeedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Links every global review type to the repository so its enabled state can be configured.
        /// Returns the number of new links that were created.
        /// </summary>
        /// <param name="repositoryId">The repository to link.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<int> LinkAllReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default);
    }
}
