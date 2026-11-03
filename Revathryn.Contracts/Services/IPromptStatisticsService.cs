using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Provides basic prompt effectiveness statistics for activation decisions.</summary>
    public interface IPromptStatisticsService
    {
        /// <summary>Returns basic statistics filtered by review type, prompt version, model and date range.</summary>
        Task<PromptStatistics> GetStatisticsAsync(PromptStatisticsFilter filter, CancellationToken cancellationToken = default);
    }
}
