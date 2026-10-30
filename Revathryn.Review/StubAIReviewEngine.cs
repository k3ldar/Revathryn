using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Review
{
    /// <summary>Initial stub for the AI review engine. AI calls are implemented in a later phase.</summary>
    public sealed class StubAIReviewEngine : IAIReviewEngine
    {
        private const string NotImplementedMessage = "AI review has not been implemented in this phase.";

        /// <summary>Returns a response indicating that AI review is not yet available.</summary>
        public Task<ReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
        {
            ReviewResponse response = new()
            {
                Success = false,
                Error = NotImplementedMessage
            };

            return Task.FromResult(response);
        }
    }
}
