using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Review
{
    /// <summary>Initial stub for context provision. Symbol context wiring lands in a later phase.</summary>
    public sealed class StubReviewContextProvider : IReviewContextProvider
    {
        /// <summary>Returns an empty context response for the requested key.</summary>
        public Task<ReviewContextResponse> GetContextAsync(ReviewContextRequest request, CancellationToken cancellationToken = default)
        {
            ReviewContextResponse response = new()
            {
                ContextKey = request.ContextKey
            };

            return Task.FromResult(response);
        }
    }
}
