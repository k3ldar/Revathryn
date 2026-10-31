using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Persists a complete review outcome atomically.</summary>
    public interface IReviewOutcomeStore
    {
        /// <summary>Persists findings and completed analysis in a single transaction.</summary>
        Task PersistAsync(ReviewOutcome outcome, CancellationToken cancellationToken = default);
    }
}
