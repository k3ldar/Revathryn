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
        private const string ResponseTypeJsonObject = "json_object";
        private const string CodeFence = "```";
        private const string FileContentHeader = "File content:";
        private const string AdditionalContextHeader = "Additional context:";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IAIEngineRepository _engineRepository;
        private readonly ISecretsProvider _secretsProvider;
        private readonly HttpClient _httpClient;

        /// <summary>Initialises a new OpenAI-compatible review engine.</summary>
        public OpenAIReviewEngine(
            IAIEngineRepository engineRepository,
            ISecretsProvider secretsProvider,
            HttpClient httpClient)
        {
            _engineRepository = engineRepository;
            _secretsProvider = secretsProvider;
            _httpClient = httpClient;
        }

        /// <summary>Runs a review request and returns the resulting findings.</summary>
        public async Task<ReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            AIEngine engine = await ResolveEngineAsync(cancellationToken).ConfigureAwait(false);

            if (engine is null)
            {
                return Failure(Strings.AIEngineNotConfigured);
            }

            string apiKey = await _secretsProvider.GetSecretAsync(engine.SecretKeyName, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Failure(string.Format(Strings.AIEngineSecretMissing, engine.Name));
            }

            string requestJson = BuildRequestJson(engine, request);

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, BuildEndpoint(engine.Endpoint));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, apiKey);
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
                return Failure(exception.Message);
            }

            string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return Failure(ExtractApiError(body, httpResponse.StatusCode));
            }

            return ParseReviewOutput(body);
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

        private static string BuildRequestJson(AIEngine engine, ReviewRequest request)
        {
            OpenAIChatCompletionRequest payload = new()
            {
                Model = engine.Model,
                Messages =
                [
                    new OpenAIMessage { Role = SystemRole, Content = request.Prompt ?? string.Empty },
                    new OpenAIMessage { Role = UserRole, Content = BuildUserContent(request) }
                ],
                ResponseFormat = new OpenAIResponseFormat { Type = ResponseTypeJsonObject },
                Temperature = 0
            };

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static string BuildUserContent(ReviewRequest request)
        {
            StringBuilder builder = new();
            builder.AppendLine(FileContentHeader);
            builder.AppendLine(request.FileContent ?? string.Empty);

            if (request.AdditionalContext is not null && request.AdditionalContext.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(AdditionalContextHeader);

                foreach (string contextItem in request.AdditionalContext)
                {
                    builder.AppendLine(contextItem);
                }
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
                    return Failure(Strings.AIEngineResponseIncomplete);
                }

                string content = completion.Choices[0].Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    return Failure(Strings.AIEngineResponseIncomplete);
                }

                ReviewOutput output = JsonSerializer.Deserialize<ReviewOutput>(ExtractJsonContent(content));

                if (output?.Results is null)
                {
                    return Failure(Strings.AIEngineResponseIncomplete);
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
                        SuggestedFix = result.SuggestedFix ?? string.Empty
                    });
                }

                return response;
            }
            catch (JsonException)
            {
                return Failure(Strings.AIEngineResponseInvalid);
            }
        }

        private static string ExtractJsonContent(string content)
        {
            string trimmed = content.Trim();

            if (!trimmed.StartsWith(CodeFence, StringComparison.Ordinal))
            {
                return trimmed;
            }

            int start = trimmed.IndexOf(NewLine);

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
            return new ReviewResponse
            {
                Success = false,
                Error = error ?? Strings.UnknownReviewError
            };
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
        }
    }
}
