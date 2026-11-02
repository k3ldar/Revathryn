using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for the independent review engine settings.</summary>
    public interface IReviewEngineSettingsRepository
    {
        /// <summary>Returns the saved settings, or null when none have been saved.</summary>
        Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts or replaces the settings.</summary>
        Task SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default);
    }
}
