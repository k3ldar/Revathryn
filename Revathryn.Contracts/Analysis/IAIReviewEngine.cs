using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>Executes AI-driven reviews and returns structured findings.</summary>
    public interface IAIReviewEngine
    {
        /// <summary>Runs a review request and returns the resulting findings.</summary>
        Task<ReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default);
    }
}
