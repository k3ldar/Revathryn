using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Review
{
    /// <summary>
    /// Sends chat completion requests to an OpenAI-compatible endpoint and classifies failures so callers can
    /// decide whether to retry. Credentials are never included in results, logs or raw response bodies.
    /// </summary>
    public sealed class OpenAICompletionClient : IAICompletionClient
    {
        private const string JsonMediaType = "application/json";
        private const string BearerScheme = "Bearer";
        private const string SystemRole = "system";
        private const string UserRole = "user";
        private const string RedactedValue = "***";

        private readonly ISecretsProvider _secretsProvider;
        private readonly HttpClient _httpClient;
        private readonly IOperationalDiagnostics _diagnostics;
        private readonly IKeyedConcurrencyLimiter _limiter;

        /// <summary>Initialises a new client.</summary>
        public OpenAICompletionClient(ISecretsProvider secretsProvider, HttpClient httpClient, IOperationalDiagnostics diagnostics, IKeyedConcurrencyLimiter limiter)
        {
            _secretsProvider = secretsProvider;
            _httpClient = httpClient;
            _diagnostics = diagnostics;
            _limiter = limiter;
        }

        /// <summary>Executes the request. Failures are reported in the result rather than thrown.</summary>
        public async Task<AICompletionResult> CompleteAsync(AICompletionRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ReviewEngineSettings settings = request.Settings;

            if (settings is null || string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.Model))
            {
                return Failure(AICompletionFailureKind.Configuration, Strings.ReviewEngineConfigurationInvalid);
            }

            string apiKey = await ResolveApiKeyAsync(settings, cancellationToken).ConfigureAwait(false);

            if (settings.Provider is not AIProvider.Local && string.IsNullOrWhiteSpace(apiKey))
            {
                return Failure(AICompletionFailureKind.Configuration, Strings.ReviewEngineSecretMissing);
            }

            using IDisposable permit = await _limiter.AcquireAsync(Consts.ReviewEngineConnectionKey, settings.MaxSimultaneousConnections, cancellationToken).ConfigureAwait(false);

            using HttpRequestMessage httpRequest = BuildHttpRequest(request, apiKey);

            HttpResponseMessage httpResponse;

            try
            {
                httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is TaskCanceledException or TimeoutException)
            {
                return Failure(AICompletionFailureKind.Timeout, Strings.AICompletionTimedOut);
            }
            catch (HttpRequestException exception)
            {
                _diagnostics.Log(OperationalLogLevel.Warning, Consts.OpenAICompletionClientComponent, Consts.CompleteOperation, exception.GetType().Name);
                return Failure(AICompletionFailureKind.Transient, string.Format(Strings.AICompletionNetworkError, Redact(exception.Message, apiKey)));
            }

            using (httpResponse)
            {
                string body = Redact(await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), apiKey);

                return httpResponse.IsSuccessStatusCode
                    ? ParseSuccess(body)
                    : ParseFailure(httpResponse, body, apiKey);
            }
        }

        private async Task<string> ResolveApiKeyAsync(ReviewEngineSettings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.SecretKeyName))
            {
                return string.Empty;
            }

            string key = await _secretsProvider.GetSecretAsync(settings.SecretKeyName, cancellationToken).ConfigureAwait(false);
            return key ?? string.Empty;
        }

        private static HttpRequestMessage BuildHttpRequest(AICompletionRequest request, string apiKey)
        {
            string endpoint = string.Concat(request.Settings.Endpoint.Trim().TrimEnd(Consts.CharForwardSlash), Consts.OpenAIChatCompletionsSuffix);
            HttpRequestMessage httpRequest = new(HttpMethod.Post, endpoint);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, apiKey);
            }

            httpRequest.Content = new StringContent(BuildPayload(request), Encoding.UTF8, JsonMediaType);
            return httpRequest;
        }

        private static string BuildPayload(AICompletionRequest request)
        {
            ReviewEngineSettings settings = request.Settings;

            JsonObject payload = new()
            {
                [Consts.RequestKeyModel] = settings.Model,
                [Consts.RequestKeyMessages] = new JsonArray(
                    CreateMessage(SystemRole, request.SystemPrompt),
                    CreateMessage(UserRole, request.UserPrompt)),
                [Consts.RequestKeyMaxCompletionTokens] = settings.MaxTokens > 0 ? settings.MaxTokens : Consts.DefaultReviewEngineMaxTokens
            };

            if (request.RequireJson)
            {
                payload[Consts.RequestKeyResponseFormat] = new JsonObject { [Consts.ResponseFormatKeyType] = Consts.OpenAIResponseFormatJsonObject };
            }

            if (settings.Temperature.HasValue)
            {
                payload[Consts.RequestKeyTemperature] = settings.Temperature.Value;
            }

            if (!string.IsNullOrWhiteSpace(settings.ReasoningEffort))
            {
                payload[Consts.RequestKeyReasoningEffort] = settings.ReasoningEffort;
            }

            MergeAdditionalOptions(payload, settings.AdditionalOptions);
            return payload.ToJsonString();
        }

        private static JsonObject CreateMessage(string role, string content)
        {
            return new JsonObject
            {
                [Consts.MessageKeyRole] = role,
                [Consts.MessageKeyContent] = content ?? string.Empty
            };
        }

        private static void MergeAdditionalOptions(JsonObject payload, string additionalOptions)
        {
            if (string.IsNullOrWhiteSpace(additionalOptions))
            {
                return;
            }

            try
            {
                if (JsonNode.Parse(additionalOptions) is not JsonObject options)
                {
                    return;
                }

                foreach (KeyValuePair<string, JsonNode> option in options)
                {
                    if (!payload.ContainsKey(option.Key) && !IsReservedKey(option.Key))
                    {
                        payload[option.Key] = option.Value?.DeepClone();
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid options are rejected when the configuration is saved; ignore any that slipped through.
            }
        }

        private static bool IsReservedKey(string key)
        {
            return string.Equals(key, Consts.RequestKeyStream, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyModel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyMessages, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyResponseFormat, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyMaxCompletionTokens, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyTemperature, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Consts.RequestKeyReasoningEffort, StringComparison.OrdinalIgnoreCase);
        }

        private static AICompletionResult ParseSuccess(string body)
        {
            try
            {
                CompletionResponse completion = JsonSerializer.Deserialize<CompletionResponse>(body);
                string content = completion?.Choices is { Count: > 0 } ? completion.Choices[0].Message?.Content : null;

                if (string.IsNullOrWhiteSpace(content))
                {
                    return Failure(AICompletionFailureKind.Rejected, Strings.AIEngineResponseIncomplete, body);
                }

                return new AICompletionResult
                {
                    Success = true,
                    Content = content,
                    RawBody = body,
                    Usage = ReadUsage(completion.Usage)
                };
            }
            catch (JsonException)
            {
                return Failure(AICompletionFailureKind.Rejected, Strings.AIEngineResponseInvalid, body);
            }
        }

        private static AICompletionResult ParseFailure(HttpResponseMessage response, string body, string apiKey)
        {
            HttpStatusCode status = response.StatusCode;

            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failure(AICompletionFailureKind.Authentication, Strings.AIEngineAuthenticationFailed, body);
            }

            string message = ExtractApiError(body, status, apiKey);

            if (status is HttpStatusCode.TooManyRequests)
            {
                AICompletionResult limited = Failure(AICompletionFailureKind.RateLimited, message, body);
                limited.RetryAfter = ReadRetryAfter(response);
                return limited;
            }

            if (status is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout)
            {
                return Failure(AICompletionFailureKind.Timeout, Strings.AICompletionTimedOut, body);
            }

            if ((int)status >= (int)HttpStatusCode.InternalServerError)
            {
                AICompletionResult serverError = Failure(AICompletionFailureKind.Transient, message, body);
                serverError.RetryAfter = ReadRetryAfter(response);
                return serverError;
            }

            return Failure(AICompletionFailureKind.Rejected, message, body);
        }

        private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
        {
            RetryConditionHeaderValue retryAfter = response.Headers.RetryAfter;

            if (retryAfter?.Delta is TimeSpan delta)
            {
                return delta;
            }

            if (retryAfter?.Date is DateTimeOffset date)
            {
                TimeSpan wait = date - DateTimeOffset.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }

            return null;
        }

        private static string ExtractApiError(string body, HttpStatusCode status, string apiKey)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    CompletionResponse parsed = JsonSerializer.Deserialize<CompletionResponse>(body);

                    if (!string.IsNullOrWhiteSpace(parsed?.Error?.Message))
                    {
                        return Redact(parsed.Error.Message, apiKey);
                    }
                }
                catch (JsonException)
                {
                    // Fall through to the generic message.
                }
            }

            return string.Format(Strings.AIEngineHttpError, (int)status);
        }

        private static TokenUsage ReadUsage(CompletionUsage usage)
        {
            return new TokenUsage
            {
                AttemptCount = 1,
                PromptTokens = Math.Max(0, usage?.PromptTokens ?? 0),
                CompletionTokens = Math.Max(0, usage?.CompletionTokens ?? 0),
                TotalTokens = Math.Max(0, usage?.TotalTokens ?? 0)
            };
        }

        private static string Redact(string text, string apiKey)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(apiKey))
            {
                return text ?? string.Empty;
            }

            return text.Replace(apiKey, RedactedValue, StringComparison.Ordinal);
        }

        private static AICompletionResult Failure(AICompletionFailureKind kind, string message, string rawBody = "")
        {
            return new AICompletionResult
            {
                Success = false,
                FailureKind = kind,
                ErrorMessage = message,
                RawBody = rawBody ?? string.Empty
            };
        }

        private sealed class CompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<CompletionChoice> Choices { get; set; }

            [JsonPropertyName("error")]
            public CompletionError Error { get; set; }

            [JsonPropertyName("usage")]
            public CompletionUsage Usage { get; set; }
        }

        private sealed class CompletionChoice
        {
            [JsonPropertyName("message")]
            public CompletionMessage Message { get; set; }
        }

        private sealed class CompletionMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private sealed class CompletionError
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        private sealed class CompletionUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }
    }
}
