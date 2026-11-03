using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Review
{
    /// <summary>
    /// Disagreement analysis engine. Sends the analysis prompts through the configured completion client and
    /// converts the structured response. It never approves or activates its own recommendations.
    /// </summary>
    public sealed class AIPromptAnalysisEngine : IPromptAnalysisEngine
    {
        private readonly IAICompletionClient _completionClient;

        /// <summary>Initialises a new engine.</summary>
        public AIPromptAnalysisEngine(IAICompletionClient completionClient)
        {
            _completionClient = completionClient;
        }

        /// <summary>Runs the analysis. Provider failures and malformed output are returned as unsuccessful results.</summary>
        public async Task<PromptAnalysisEngineResult> AnalyseAsync(PromptAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            AICompletionResult completion = await _completionClient.CompleteAsync(
                new AICompletionRequest
                {
                    Settings = request.Settings,
                    SystemPrompt = request.SystemPrompt,
                    UserPrompt = request.UserPrompt,
                    RequireJson = true
                },
                cancellationToken).ConfigureAwait(false);

            if (!completion.Success)
            {
                return new PromptAnalysisEngineResult
                {
                    Success = false,
                    FailureKind = completion.FailureKind,
                    ErrorMessage = completion.ErrorMessage,
                    RetryAfter = completion.RetryAfter,
                    RawResponse = completion.RawBody,
                    Usage = completion.Usage
                };
            }

            PromptAnalysisEngineResult result = PromptAnalysisResponseParser.Parse(completion.Content);
            result.Usage = completion.Usage;
            return result;
        }
    }
}
