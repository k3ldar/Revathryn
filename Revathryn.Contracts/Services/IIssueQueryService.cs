using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Read-side queries for issues identified by Revathryn.</summary>
    public interface IIssueQueryService
    {
        /// <summary>Returns a filtered, sorted and paged set of findings.</summary>
        Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default);

        /// <summary>Returns aggregated outstanding issue counts and recent activity.</summary>
        Task<IssueSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    }
}
