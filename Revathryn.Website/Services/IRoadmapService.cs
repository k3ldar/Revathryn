using Revathryn.Website.Models;

namespace Revathryn.Website.Services
{
    /// <summary>Provides access to the project roadmap.</summary>
    public interface IRoadmapService
    {
        /// <summary>Gets the roadmap asynchronously.</summary>
        Task<RoadmapData> GetRoadmapAsync(CancellationToken cancellationToken = default);
    }
}
