using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Read-side queries supplying the operational dashboard.</summary>
    public interface IDashboardQueryService
    {
        /// <summary>Returns the aggregated dashboard snapshot.</summary>
        Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    }
}
