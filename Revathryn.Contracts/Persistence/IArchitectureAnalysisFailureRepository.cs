using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for persisted architecture pass failures.</summary>
    public interface IArchitectureAnalysisFailureRepository
    {
        /// <summary>Inserts the failure and returns its assigned id.</summary>
        Task<long> AddAsync(ArchitectureAnalysisFailure failure, CancellationToken cancellationToken = default);

        /// <summary>Returns all failures, most recent first.</summary>
        Task<IReadOnlyList<ArchitectureAnalysisFailure>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the failure with the given id, or null when not found.</summary>
        Task<ArchitectureAnalysisFailure> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    }
}
