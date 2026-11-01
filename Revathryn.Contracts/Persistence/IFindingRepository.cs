using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for review findings.</summary>
    public interface IFindingRepository
    {
        /// <summary>Returns the finding with the given id, or null when not found.</summary>
        Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all findings belonging to a repository.</summary>
        Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Returns the first finding matching the repository and fingerprint, or null.</summary>
        Task<Finding> GetByFingerprintAsync(long repositoryId, string fingerprint, CancellationToken cancellationToken = default);

        /// <summary>Returns a filtered, sorted and paged set of findings.</summary>
        Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default);

        /// <summary>Returns all outstanding, non-operational findings, most recent first.</summary>
        Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts the finding and returns its assigned id.</summary>
        Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing finding.</summary>
        Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default);

        /// <summary>Deletes the finding with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
