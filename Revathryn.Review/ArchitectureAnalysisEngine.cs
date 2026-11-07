using System.Text;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Review
{
    /// <summary>
    /// Produces free-text descriptive structure summaries used to prime reviews.
    /// This engine owns the overview concern and is deliberately separate from
    /// <see cref="IAIReviewEngine"/>, which produces strict-JSON review findings.
    /// </summary>
    public sealed class ArchitectureAnalysisEngine : IArchitectureAnalysisEngine
    {
        private const string FileContentHeader = "File content:";

        private readonly IAIEngineRepository _engineRepository;
        private readonly IAICompletionClient _completionClient;

        /// <summary>Initialises a new architecture analysis engine.</summary>
        public ArchitectureAnalysisEngine(IAIEngineRepository engineRepository, IAICompletionClient completionClient)
        {
            _engineRepository = engineRepository;
            _completionClient = completionClient;
        }

        /// <inheritdoc />
        public async Task<ArchitectureAnalysisResponse> AnalyseStructureAsync(ArchitectureAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            AIEngine engine = await ResolveEngineAsync(cancellationToken).ConfigureAwait(false);

            if (engine is null)
            {
                return Failure(Strings.AIEngineNotConfigured, null, new TokenUsage(), string.Empty);
            }

            if (!engine.UseArchitecturePass)
            {
                return Skipped(engine);
            }

            AICompletionResult completion = await _completionClient.CompleteAsync(
                new AICompletionRequest
                {
                    Settings = CreateSettings(engine),
                    SystemPrompt = request.Prompt ?? string.Empty,
                    UserPrompt = BuildUserContent(request),
                    RequireJson = false
                },
                cancellationToken).ConfigureAwait(false);

            if (!completion.Success)
            {
                return Failure(completion.ErrorMessage, engine, completion.Usage, completion.RawBody);
            }

            if (string.IsNullOrWhiteSpace(completion.Content))
            {
                return Failure(Strings.AIEngineResponseIncomplete, engine, completion.Usage, completion.RawBody);
            }

            return new ArchitectureAnalysisResponse
            {
                Success = true,
                Summary = completion.Content,
                RawResponse = completion.RawBody,
                EngineId = engine.Id,
                EngineName = engine.Name,
                EngineProvider = engine.Provider.ToString(),
                EngineModel = engine.Model,
                Usage = completion.Usage
            };
        }

        private async Task<AIEngine> ResolveEngineAsync(CancellationToken cancellationToken)
        {
            AIEngine engine = await _engineRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (engine is not null && engine.Enabled)
            {
                return engine;
            }

            IReadOnlyList<AIEngine> enabledEngines = await _engineRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
            return enabledEngines.Count == 0 ? null : enabledEngines[0];
        }

        private static string BuildUserContent(ArchitectureAnalysisRequest request)
        {
            StringBuilder builder = new();
            builder.AppendLine(FileContentHeader);
            builder.AppendLine(request.FileContent ?? string.Empty);

            return builder.ToString();
        }

        private static ReviewEngineSettings CreateSettings(AIEngine engine)
        {
            return new ReviewEngineSettings
            {
                Provider = engine.Provider,
                Endpoint = engine.Endpoint ?? string.Empty,
                Model = engine.Model ?? string.Empty,
                Temperature = 0,
                MaxTokens = engine.MaxTokens,
                SecretKeyName = engine.SecretKeyName ?? string.Empty,
                Enabled = engine.Enabled
            };
        }

        private static ArchitectureAnalysisResponse Skipped(AIEngine engine)
        {
            return new ArchitectureAnalysisResponse
            {
                Success = true,
                Skipped = true,
                EngineId = engine.Id,
                EngineName = engine.Name,
                EngineProvider = engine.Provider.ToString(),
                EngineModel = engine.Model,
                Usage = new TokenUsage()
            };
        }

        private static ArchitectureAnalysisResponse Failure(string error, AIEngine engine, TokenUsage usage, string rawResponse)
        {
            ArchitectureAnalysisResponse response = new()
            {
                Success = false,
                Error = error ?? Strings.UnknownReviewError,
                RawResponse = rawResponse ?? string.Empty,
                Usage = usage ?? new TokenUsage()
            };

            if (engine is null)
            {
                return response;
            }

            response.EngineId = engine.Id;
            response.EngineName = engine.Name;
            response.EngineProvider = engine.Provider.ToString();
            response.EngineModel = engine.Model;

            return response;
        }
    }
}
