using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for global AI settings, including the master prompt.</summary>
    public interface IAISettingsRepository
    {
        /// <summary>Returns the global AI settings, creating defaults when absent.</summary>
        Task<AISettings> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Persists the global AI settings.</summary>
        Task UpdateAsync(AISettings settings, CancellationToken cancellationToken = default);
    }
}
