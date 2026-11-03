using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Review
{
    /// <summary>
    /// Prompt proposal engine. Sends the proposal prompts through the configured completion client and converts the
    /// structured response. The resulting recommendations are always drafts pending explicit user approval.
    /// </summary>
    public sealed class AIPromptProposalEngine : IPromptProposalEngine
    {
        private readonly IAICompletionClient _completionClient;

        /// <summary>Initialises a new engine.</summary>
        public AIPromptProposalEngine(IAICompletionClient completionClient)
        {
            _completionClient = completionClient;
        }

        /// <summary>Generates recommendations. Provider failures and malformed output are returned as unsuccessful results.</summary>
        public async Task<PromptProposalEngineResult> GenerateAsync(PromptProposalRequest request, CancellationToken cancellationToken = default)
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
                return new PromptProposalEngineResult
                {
                    Success = false,
                    FailureKind = completion.FailureKind,
                    ErrorMessage = completion.ErrorMessage,
                    RetryAfter = completion.RetryAfter,
                    RawResponse = completion.RawBody,
                    Usage = completion.Usage
                };
            }

            PromptProposalEngineResult result = PromptProposalResponseParser.Parse(completion.Content);
            result.Usage = completion.Usage;
            return result;
        }
    }
}
