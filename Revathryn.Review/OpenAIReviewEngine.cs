using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Review
{
    /// <summary>
    /// Executes AI reviews against an OpenAI-compatible chat completions endpoint.
    /// Responses are strictly validated so that incomplete or malformed model output
    /// is reported as a failure rather than a successful review.
    /// </summary>
    public sealed class OpenAIReviewEngine : IAIReviewEngine
    {
        private const string JsonMediaType = "application/json";
        private const string BearerScheme = "Bearer";
        private const string SystemRole = "system";
        private const string UserRole = "user";
        private const string AssistantRole = "assistant";
        private const string ResponseTypeJsonObject = "json_object";
        private const string CodeFence = "```";
        private const string FileContentHeader = "File content:";
        private const string AdditionalContextHeader = "Additional context:";
        private const string RequestedContextHeader = "Requested context:";
        private const string PromptArchitectureSummary = "Please provide a summary of the architecture of the code in the file content.";
        private const int PromptPosition1 = 1;
        private const int PromptPosition2 = 2;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IAIEngineRepository _engineRepository;
        private readonly ISecretsProvider _secretsProvider;
        private readonly HttpClient _httpClient;
        private readonly IOperationalDiagnostics _diagnostics;
        private readonly IKeyedConcurrencyLimiter _limiter;

        /// <summary>Initialises a new OpenAI-compatible review engine.</summary>
        public OpenAIReviewEngine(
            IAIEngineRepository engineRepository,
            ISecretsProvider secretsProvider,
            HttpClient httpClient,
            IOperationalDiagnostics diagnostics,
            IKeyedConcurrencyLimiter limiter)
        {
            _engineRepository = engineRepository;
            _secretsProvider = secretsProvider;
            _httpClient = httpClient;
            _diagnostics = diagnostics;
            _limiter = limiter;
        }

        /// <summary>Runs a review request and returns the resulting findings.</summary>
        public async Task<ReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            AIEngine engine = await ResolveEngineAsync(request, cancellationToken).ConfigureAwait(false);

            if (engine is null)
            {
                return ConfigurationFailure(Strings.AIEngineNotConfigured);
            }

            string apiKey = await _secretsProvider.GetSecretAsync(engine.SecretKeyName, cancellationToken).ConfigureAwait(false) ?? string.Empty;

            if (engine.Provider is not AIProvider.Local && string.IsNullOrWhiteSpace(apiKey))
            {
                return ConfigurationFailure(Strings.ReviewEngineSecretMissing, engine);
            }

            List<OpenAIMessage> correctionMessages = null;
            TokenUsage usage = new();

            for (int attempt = 0; ; attempt++)
            {
                string requestJson = BuildRequestJson(engine, request, correctionMessages);

                SendResult sendResult = await SendReviewRequestAsync(engine, apiKey, requestJson, cancellationToken).ConfigureAwait(false);

                if (!sendResult.Success)
                {
                    return Failure(sendResult.Error, engine, usage);
                }

                AccumulateUsage(usage, sendResult.Body);

                try
                {
                    ReviewResponse response = ParseReviewOutput(sendResult.Body);
                    AttachEngine(response, engine, usage);
                    return response;
                }
                catch (InvalidAIResponseException exception) when (attempt < Consts.AIReviewCorrectionMaxAttempts)
                {
                    _diagnostics.Log(
                        OperationalLogLevel.Warning,
                        Consts.OpenAIReviewEngineComponent,
                        Consts.ReviewOperation,
                        exception.Message,
                        exception,
                        retryAttempt: attempt + 1);

                    correctionMessages = BuildCorrectionMessages(sendResult.Body, exception);
                }
                catch (InvalidAIResponseException exception)
                {
                    _diagnostics.Log(
                        OperationalLogLevel.Error,
                        Consts.OpenAIReviewEngineComponent,
                        Consts.ReviewOperation,
                        exception.Message,
                        exception);

                    return Failure(exception.Message, engine, usage);
                }
            }
        }

        private async Task<AIEngine> ResolveEngineAsync(ReviewRequest request, CancellationToken cancellationToken)
        {
            if (request.AIEngineId is long engineId)
            {
                AIEngine selected = await _engineRepository.GetByIdAsync(engineId, cancellationToken).ConfigureAwait(false);

                if (selected is not null)
                {
                    return selected;
                }
            }

            AIEngine engine = await _engineRepository.GetDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (engine is not null && engine.Enabled)
            {
                return engine;
            }

            IReadOnlyList<AIEngine> enabledEngines = await _engineRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
            return enabledEngines.Count == 0 ? null : enabledEngines[0];
        }

        private static string BuildRequestJson(AIEngine engine, ReviewRequest request, List<OpenAIMessage> correctionMessages = null)
        {
            List<OpenAIMessage> messages =
            [
                new OpenAIMessage { Role = SystemRole, Content = request.Prompt ?? string.Empty },
            ];

            if (!string.IsNullOrWhiteSpace(request.ArchitectureSummary))
            {
                messages.Insert(PromptPosition1, new OpenAIMessage { Role = UserRole, Content = PromptArchitectureSummary });
                messages.Insert(PromptPosition2, new OpenAIMessage { Role = AssistantRole, Content = request.ArchitectureSummary });
            }

            messages.Add(new OpenAIMessage { Role = UserRole, Content = BuildUserContent(request) });

            if (request.ContextRounds is not null)
            {
                foreach (ReviewContextRound round in request.ContextRounds)
                {
                    messages.Add(new OpenAIMessage { Role = UserRole, Content = BuildContextRoundContent(round) });
                }
            }

            if (correctionMessages is not null && correctionMessages.Count > 0)
            {
                messages.AddRange(correctionMessages);
            }

            OpenAIChatCompletionRequest payload = new()
            {
                Model = engine.Model,
                Messages = messages,
                ResponseFormat = new OpenAIResponseFormat { Type = ResponseTypeJsonObject },
                Temperature = 0,
                MaxCompletionTokens = engine.MaxTokens > 0 ? engine.MaxTokens : DefaultAIMaxTokens
            };

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static string BuildUserContent(ReviewRequest request)
        {
            StringBuilder builder = new();
            builder.AppendLine(FileContentHeader);
            builder.AppendLine(request.FileContent ?? string.Empty);

            if (request.InitialContext is not null && request.InitialContext.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(AdditionalContextHeader);

                foreach (string contextItem in request.InitialContext)
                {
                    builder.AppendLine(contextItem);
                }
            }

            return builder.ToString();
        }

        private static string BuildContextRoundContent(ReviewContextRound round)
        {
            StringBuilder builder = new();
            builder.AppendLine(RequestedContextHeader);

            foreach (string contextItem in round.Items ?? [])
            {
                builder.AppendLine(contextItem);
            }

            return builder.ToString();
        }

        private static string BuildEndpoint(string endpoint)
        {
            string baseEndpoint = (endpoint ?? string.Empty).TrimEnd(Consts.CharForwardSlash);
            return string.Concat(baseEndpoint, Consts.OpenAIChatCompletionsSuffix);
        }

        private static ReviewResponse ParseReviewOutput(string body)
        {
            try
            {
                OpenAIChatCompletionResponse completion = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(body);

                if (completion?.Choices is null || completion.Choices.Count == 0)
                {
                    throw new InvalidAIResponseException(Strings.AIEngineResponseIncomplete);
                }

                string content = completion.Choices[0].Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidAIResponseException(Strings.AIEngineResponseIncomplete);
                }

                ReviewOutput output = JsonSerializer.Deserialize<ReviewOutput>(ExtractJsonContent(content));

                if (output?.Results is null)
                {
                    throw new InvalidAIResponseException(Strings.AIEngineResponseIncomplete);
                }

                ReviewResponse response = new()
                {
                    Success = true,
                    ContextRequests = output.ContextRequests ?? []
                };

                foreach (ReviewResultOutput result in output.Results)
                {
                    response.Results.Add(new ReviewResult
                    {
                        Fingerprint = result.Fingerprint ?? string.Empty,
                        Severity = ParseSeverity(result.Severity),
                        Title = result.Title ?? string.Empty,
                        Description = result.Description ?? string.Empty,
                        SuggestedFix = result.SuggestedFix ?? string.Empty,
                        FindingKey = result.FindingKey ?? string.Empty,
                        LineStart = result.LineStart,
                        LineEnd = result.LineEnd
                    });
                }

                return response;
            }
            catch (JsonException exception)
            {
                throw new InvalidAIResponseException(Strings.AIEngineResponseInvalid, exception);
            }
        }

        private async Task<SendResult> SendReviewRequestAsync(AIEngine engine, string apiKey, string requestJson, CancellationToken cancellationToken)
        {
            string connectionKey = Consts.DetectionEngineConnectionKeyPrefix + engine.Id;

            using IDisposable permit = await _limiter.AcquireAsync(connectionKey, engine.MaxSimultaneousConnections, cancellationToken).ConfigureAwait(false);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, BuildEndpoint(engine.Endpoint));

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, apiKey);
            }

            httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, JsonMediaType);

            HttpResponseMessage httpResponse;

            try
            {
                httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return SendResult.Failed(exception.Message);
            }

            string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return SendResult.Failed(ExtractApiError(body, httpResponse.StatusCode));
            }

            return SendResult.Succeeded(body);
        }

        private static List<OpenAIMessage> BuildCorrectionMessages(string body, InvalidAIResponseException exception)
        {
            return
            [
                new OpenAIMessage { Role = AssistantRole, Content = ExtractAssistantContent(body) },
                new OpenAIMessage { Role = UserRole, Content = BuildCorrectionPrompt(exception) }
            ];
        }

        private static string ExtractAssistantContent(string body)
        {
            try
            {
                OpenAIChatCompletionResponse completion = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(body);

                if (completion?.Choices is not null && completion.Choices.Count > 0)
                {
                    string content = completion.Choices[0].Message?.Content;

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        return content;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to echoing the raw body when the outer response cannot be parsed.
            }

            return body;
        }

        private static string BuildCorrectionPrompt(InvalidAIResponseException exception)
        {
            if (exception.JsonError is null || string.IsNullOrWhiteSpace(exception.JsonError.Message))
            {
                return Strings.AIEngineCorrectionRequest;
            }

            return string.Format(Strings.AIEngineCorrectionRequestWithDetail, exception.JsonError.Message);
        }

        private static string ExtractJsonContent(string content)
        {
            string trimmed = content.Trim();

            if (trimmed.EndsWith(CodeFence, StringComparison.Ordinal))
            {
                int closingFence = trimmed.LastIndexOf(CodeFence, StringComparison.Ordinal);

                if (closingFence > 0)
                {
                    trimmed = trimmed[..closingFence].TrimEnd();
                }
            }

            if (!trimmed.StartsWith(CodeFence, StringComparison.Ordinal))
            {
                return trimmed;
            }

            int start = trimmed.IndexOf(CharNewLine);

            if (start < 0)
            {
                return trimmed;
            }

            string withoutOpeningFence = trimmed[(start + 1)..].TrimStart();

            int end = withoutOpeningFence.LastIndexOf(CodeFence, StringComparison.Ordinal);

            if (end < 0)
            {
                return withoutOpeningFence.Trim();
            }

            return withoutOpeningFence[..end].Trim();
        }

        private static string ExtractApiError(string body, System.Net.HttpStatusCode statusCode)
        {
            if (statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                return Strings.AIEngineAuthenticationFailed;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Format(Strings.AIEngineHttpError, (int)statusCode);
            }

            try
            {
                OpenAIChatCompletionResponse response = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(body);

                if (!string.IsNullOrWhiteSpace(response?.Error?.Message))
                {
                    return response.Error.Message;
                }
            }
            catch (JsonException)
            {
                // Fall through to the generic message
            }

            return string.Format(Strings.AIEngineHttpError, (int)statusCode);
        }

        private static FindingSeverity ParseSeverity(string value)
        {
            if (Enum.TryParse(value, ignoreCase: true, out FindingSeverity severity))
            {
                return severity;
            }

            return FindingSeverity.Medium;
        }

        private static ReviewResponse Failure(string error)
        {
            return Failure(error, null, new TokenUsage());
        }

        private static ReviewResponse ConfigurationFailure(string error, AIEngine engine = null)
        {
            ReviewResponse response = Failure(error, engine, new TokenUsage());
            response.IsConfigurationFailure = true;
            return response;
        }

        private static ReviewResponse Failure(string error, AIEngine engine, TokenUsage usage)
        {
            ReviewResponse response = new()
            {
                Success = false,
                Error = error ?? Strings.UnknownReviewError
            };

            AttachEngine(response, engine, usage);
            return response;
        }

        private static void AttachEngine(ReviewResponse response, AIEngine engine, TokenUsage usage)
        {
            response.Usage = usage ?? new TokenUsage();

            if (engine is null)
            {
                return;
            }

            response.EngineId = engine.Id;
            response.EngineName = engine.Name;
            response.EngineProvider = engine.Provider.ToString();
            response.EngineModel = engine.Model;
        }

        private static void AccumulateUsage(TokenUsage usage, string body)
        {
            usage.AttemptCount++;

            try
            {
                OpenAIChatCompletionResponse completion = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(body);
                OpenAIUsage report = completion?.Usage;

                if (report is null)
                {
                    return;
                }

                usage.PromptTokens += Math.Max(0, report.PromptTokens);
                usage.CompletionTokens += Math.Max(0, report.CompletionTokens);
                usage.TotalTokens += Math.Max(0, report.TotalTokens);
                usage.CachedTokens += Math.Max(0, ResolveCachedTokens(report));
            }
            catch (JsonException)
            {
                // Usage is best-effort; the caller handles malformed response bodies.
            }
        }

        private static int ResolveCachedTokens(OpenAIUsage usage)
        {
            if (usage.PromptTokensDetails?.CachedTokens > 0)
            {
                return usage.PromptTokensDetails.CachedTokens;
            }

            if (usage.CachedTokens > 0)
            {
                return usage.CachedTokens;
            }

            return Math.Max(0, usage.CacheReadInputTokens);
        }

        private sealed class OpenAIChatCompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<OpenAIMessage> Messages { get; set; }

            [JsonPropertyName("response_format")]
            public OpenAIResponseFormat ResponseFormat { get; set; }

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("max_completion_tokens")]
            public int MaxCompletionTokens { get; set; }
        }

        private sealed class OpenAIMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private sealed class OpenAIResponseFormat
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        private sealed class OpenAIChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAIChoice> Choices { get; set; }

            [JsonPropertyName("error")]
            public OpenAIErrorBody Error { get; set; }

            [JsonPropertyName("usage")]
            public OpenAIUsage Usage { get; set; }
        }

        private sealed class OpenAIUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            [JsonPropertyName("cached_tokens")]
            public int CachedTokens { get; set; }

            [JsonPropertyName("prompt_tokens_details")]
            public OpenAIPromptTokensDetails PromptTokensDetails { get; set; }

            [JsonPropertyName("cache_read_input_tokens")]
            public int CacheReadInputTokens { get; set; }
        }

        private sealed class OpenAIPromptTokensDetails
        {
            [JsonPropertyName("cached_tokens")]
            public int CachedTokens { get; set; }
        }

        private sealed class OpenAIChoice
        {
            [JsonPropertyName("message")]
            public OpenAIAssistantMessage Message { get; set; }
        }

        private sealed class OpenAIAssistantMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private sealed class OpenAIErrorBody
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        private sealed class ReviewOutput
        {
            [JsonPropertyName("results")]
            public List<ReviewResultOutput> Results { get; set; }

            [JsonPropertyName("contextRequests")]
            public List<string> ContextRequests { get; set; }
        }

        private sealed class ReviewResultOutput
        {
            [JsonPropertyName("fingerprint")]
            public string Fingerprint { get; set; }

            [JsonPropertyName("severity")]
            public string Severity { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("suggestedFix")]
            public string SuggestedFix { get; set; }

            [JsonPropertyName("findingKey")]
            public string FindingKey { get; set; }

            [JsonPropertyName("lineStart")]
            public int? LineStart { get; set; }

            [JsonPropertyName("lineEnd")]
            public int? LineEnd { get; set; }
        }

        private sealed class SendResult
        {
            public bool Success { get; init; }

            public string Body { get; init; }

            public string Error { get; init; }

            public static SendResult Succeeded(string body)
            {
                return new() { Success = true, Body = body };
            }

            public static SendResult Failed(string error)
            {
                return new() { Success = false, Error = error };
            }
        }

        private sealed class InvalidAIResponseException : Exception
        {
            public InvalidAIResponseException(string message)
                : base(message)
            {
            }

            public InvalidAIResponseException(string message, JsonException jsonError)
                : base(message, jsonError)
            {
                JsonError = jsonError;
            }

            public JsonException JsonError { get; }
        }
    }
}
