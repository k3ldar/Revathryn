using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for basic prompt performance statistics.</summary>
    public interface IPromptStatisticsRepository
    {
        /// <summary>Returns the raw counts matching the supplied filter.</summary>
        Task<PromptStatisticsSnapshot> GetSnapshotAsync(PromptStatisticsFilter filter, CancellationToken cancellationToken = default);
    }
}
