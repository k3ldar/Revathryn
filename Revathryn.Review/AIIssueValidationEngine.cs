using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Review
{
    /// <summary>
    /// Independent validation engine. Sends the validation prompts through the configured completion client and
    /// converts the structured response. It has no knowledge of provider specifics or model names.
    /// </summary>
    public sealed class AIIssueValidationEngine : IIssueValidationEngine
    {
        private readonly IAICompletionClient _completionClient;

        /// <summary>Initialises a new engine.</summary>
        public AIIssueValidationEngine(IAICompletionClient completionClient)
        {
            _completionClient = completionClient;
        }

        /// <summary>Runs the validation. Provider failures and malformed output are returned as unsuccessful results.</summary>
        public async Task<IssueValidationEngineResult> ValidateAsync(IssueValidationRequest request, CancellationToken cancellationToken = default)
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
                return new IssueValidationEngineResult
                {
                    Success = false,
                    FailureKind = completion.FailureKind,
                    ErrorMessage = completion.ErrorMessage,
                    RetryAfter = completion.RetryAfter,
                    RawResponse = completion.RawBody,
                    Usage = completion.Usage
                };
            }

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(completion.Content, request.SuppliedSources);
            result.Usage = completion.Usage;
            return result;
        }
    }
}
