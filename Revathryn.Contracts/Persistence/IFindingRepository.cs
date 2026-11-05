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

        /// <summary>Returns the findings immediately before and after the given finding within the supplied query.</summary>
        Task<FindingNavigation> GetNavigationAsync(long findingId, FindingQuery query, CancellationToken cancellationToken = default);

        /// <summary>Returns total issue counts grouped by repository.</summary>
        Task<IReadOnlyList<RepositoryIssueCount>> CountByRepositoryAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns all outstanding findings, most recent first.</summary>
        Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the number of outstanding operational failure findings.</summary>
        Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the code-issue findings in a repository that are eligible for similarity matching.</summary>
        Task<IReadOnlyList<Finding>> GetSimilarityCandidatesAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Records that a finding duplicates another finding with the given similarity score.</summary>
        Task MarkDuplicateAsync(long findingId, long duplicateOfFindingId, double similarityScore, DateTime lastModifiedUtc, CancellationToken cancellationToken = default);

        /// <summary>Inserts the finding and returns its assigned id.</summary>
        Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing finding.</summary>
        Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default);

        /// <summary>Deletes the finding with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
