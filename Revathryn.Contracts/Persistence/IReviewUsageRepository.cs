using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for AI review usage records.</summary>
    public interface IReviewUsageRepository
    {
        /// <summary>Inserts a usage record and returns its assigned id.</summary>
        Task<long> AddAsync(ReviewUsage usage, CancellationToken cancellationToken = default);

        /// <summary>Returns all usage records, most recent first.</summary>
        Task<IReadOnlyList<ReviewUsage>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns aggregated usage metrics for the dashboard.</summary>
        Task<ReviewUsageSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    }
}
