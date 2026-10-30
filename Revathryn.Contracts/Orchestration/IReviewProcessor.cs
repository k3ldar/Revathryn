namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Processes queued review operations and persists their outcomes.</summary>
    public interface IReviewProcessor
    {
        /// <summary>Processes the next pending queue item, or returns false when the queue is empty.</summary>
        Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
    }
}
