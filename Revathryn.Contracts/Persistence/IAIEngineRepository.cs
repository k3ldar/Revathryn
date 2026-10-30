using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for AI engine configuration.</summary>
    public interface IAIEngineRepository
    {
        /// <summary>Returns the engine with the given id, or null when not found.</summary>
        Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all configured engines.</summary>
        Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns all enabled engines.</summary>
        Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the default engine, or null when none is configured.</summary>
        Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts the engine and returns its assigned id.</summary>
        Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing engine.</summary>
        Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default);

        /// <summary>Deletes the engine with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
