using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>Provides additional context about a file for use by review engines.</summary>
    public interface IReviewContextProvider
    {
        /// <summary>Returns context for the requested key, e.g. symbol information.</summary>
        Task<ReviewContextResponse> GetContextAsync(ReviewContextRequest request, CancellationToken cancellationToken = default);
    }
}
