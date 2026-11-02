using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for the original AI review context retained against review executions.</summary>
    public interface IReviewContextRepository
    {
        /// <summary>Returns the context with the given id, or null when not found.</summary>
        Task<AIReviewContext> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Inserts the context and returns its assigned id.</summary>
        Task<long> AddAsync(AIReviewContext context, CancellationToken cancellationToken = default);
    }
}
