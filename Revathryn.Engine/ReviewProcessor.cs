using Revathryn.Contracts.Orchestration;

namespace Revathryn.Engine
{
    /// <summary>
    /// Initial stub for the review processor. Full review processing, including
    /// engine dispatch, finding persistence and analysis fingerprint tracking,
    /// is implemented in a later phase.
    /// </summary>
    public sealed class ReviewProcessor : IReviewProcessor
    {
        /// <summary>Processes the next pending queue item, or returns false when the queue is empty.</summary>
        public Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
