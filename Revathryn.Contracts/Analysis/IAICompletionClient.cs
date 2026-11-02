using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>Sends a single chat completion request to an AI provider and classifies the outcome.</summary>
    public interface IAICompletionClient
    {
        /// <summary>Executes the request. Failures are reported in the result rather than thrown.</summary>
        Task<AICompletionResult> CompleteAsync(AICompletionRequest request, CancellationToken cancellationToken = default);
    }
}
