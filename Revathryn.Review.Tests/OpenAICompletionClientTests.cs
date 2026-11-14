using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;

namespace Revathryn.Review.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class OpenAICompletionClientTests
    {
        private const string ApiKey = "sk-super-secret-key";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task CompleteAsync_ReturnsContentAndUsageOnSuccess()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{\"verdict\":\"Agreed\"}"));
            OpenAICompletionClient client = CreateClient(handler);

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("{\"verdict\":\"Agreed\"}", result.Content);
            Assert.AreEqual(11, result.Usage.PromptTokens);
            Assert.AreEqual(7, result.Usage.CompletionTokens);
            Assert.AreEqual(18, result.Usage.TotalTokens);
        }

        [TestMethod]
        public async Task CompleteAsync_SendsConfiguredModelPromptsAndOptions()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler);
            AICompletionRequest request = CreateRequest();
            request.Settings.Temperature = 0.3;
            request.Settings.ReasoningEffort = "high";
            request.Settings.MaxTokens = 2048;

            _ = await client.CompleteAsync(request, TestContext.CancellationToken);

            Assert.AreEqual("https://review.test/v1/chat/completions", handler.RequestUris[0]);
            Assert.AreEqual("Bearer " + ApiKey, handler.AuthorizationHeaders[0]);

            using JsonDocument payload = JsonDocument.Parse(handler.RequestBodies[0]);
            JsonElement root = payload.RootElement;
            Assert.AreEqual("review-model", root.GetProperty("model").GetString());
            Assert.AreEqual(2048, root.GetProperty("max_completion_tokens").GetInt32());
            Assert.AreEqual(0.3, root.GetProperty("temperature").GetDouble(), 0.0001);
            Assert.AreEqual("high", root.GetProperty("reasoning_effort").GetString());
            Assert.AreEqual("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
            Assert.AreEqual("system prompt", root.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.AreEqual("user prompt", root.GetProperty("messages")[1].GetProperty("content").GetString());
        }

        [TestMethod]
        public async Task CompleteAsync_OmitsOptionalSettingsWhenNotConfigured()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler);

            _ = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            using JsonDocument payload = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.IsFalse(payload.RootElement.TryGetProperty("temperature", out _));
            Assert.IsFalse(payload.RootElement.TryGetProperty("reasoning_effort", out _));
        }

        [TestMethod]
        public async Task CompleteAsync_MergesAdditionalOptionsButNeverOverridesReservedKeys()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler);
            AICompletionRequest request = CreateRequest();
            request.Settings.AdditionalOptions = "{\"top_p\":0.5,\"model\":\"hijack\",\"stream\":true,\"messages\":[]}";

            _ = await client.CompleteAsync(request, TestContext.CancellationToken);

            using JsonDocument payload = JsonDocument.Parse(handler.RequestBodies[0]);
            JsonElement root = payload.RootElement;
            Assert.AreEqual(0.5, root.GetProperty("top_p").GetDouble(), 0.0001);
            Assert.AreEqual("review-model", root.GetProperty("model").GetString());
            Assert.IsFalse(root.TryGetProperty("stream", out _));
            Assert.AreEqual(2, root.GetProperty("messages").GetArrayLength());
        }

        [TestMethod]
        public async Task CompleteAsync_IgnoresMalformedAdditionalOptions()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler);
            AICompletionRequest request = CreateRequest();
            request.Settings.AdditionalOptions = "{not json";

            AICompletionResult result = await client.CompleteAsync(request, TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task CompleteAsync_ReportsConfigurationFailureWithoutCallingProviderWhenSettingsMissing()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler);
            AICompletionRequest request = CreateRequest();
            request.Settings.Model = string.Empty;

            AICompletionResult result = await client.CompleteAsync(request, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AICompletionFailureKind.Configuration, result.FailureKind);
            Assert.IsEmpty(handler.RequestBodies);
        }

        [TestMethod]
        public async Task CompleteAsync_ReportsConfigurationFailureWhenSettingsAreNull()
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}")));

            AICompletionResult result = await client.CompleteAsync(new AICompletionRequest(), TestContext.CancellationToken);

            Assert.AreEqual(AICompletionFailureKind.Configuration, result.FailureKind);
        }

        [TestMethod]
        public async Task CompleteAsync_ReportsConfigurationFailureWhenSecretCannotBeResolved()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler, secret: null);

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.AreEqual(AICompletionFailureKind.Configuration, result.FailureKind);
            Assert.IsEmpty(handler.RequestBodies);
        }

        [TestMethod]
        public async Task CompleteAsync_AllowsLocalProviderWithoutSecret()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.OK, SuccessBody("{}"));
            OpenAICompletionClient client = CreateClient(handler, secret: null);
            AICompletionRequest request = CreateRequest();
            request.Settings.Provider = AIProvider.Local;
            request.Settings.SecretKeyName = string.Empty;

            AICompletionResult result = await client.CompleteAsync(request, TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
            Assert.IsNull(handler.AuthorizationHeaders[0]);
        }

        [TestMethod]
        public async Task CompleteAsync_ClassifiesRateLimitingAndReadsRetryAfter()
        {
            RecordingHandler handler = RecordingHandler.Responding(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"slow down\"}}");
            handler.RetryAfterSeconds = 42;
            OpenAICompletionClient client = CreateClient(handler);

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AICompletionFailureKind.RateLimited, result.FailureKind);
            Assert.AreEqual(TimeSpan.FromSeconds(42), result.RetryAfter);
            Assert.AreEqual("slow down", result.ErrorMessage);
        }

        [TestMethod]
        public async Task CompleteAsync_ClassifiesRateLimitingWithoutRetryAfterHeader()
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.TooManyRequests, string.Empty));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.AreEqual(AICompletionFailureKind.RateLimited, result.FailureKind);
            Assert.IsNull(result.RetryAfter);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.InternalServerError, AICompletionFailureKind.Transient)]
        [DataRow(HttpStatusCode.BadGateway, AICompletionFailureKind.Transient)]
        [DataRow(HttpStatusCode.ServiceUnavailable, AICompletionFailureKind.Transient)]
        [DataRow(HttpStatusCode.GatewayTimeout, AICompletionFailureKind.Timeout)]
        [DataRow(HttpStatusCode.RequestTimeout, AICompletionFailureKind.Timeout)]
        [DataRow(HttpStatusCode.Unauthorized, AICompletionFailureKind.Authentication)]
        [DataRow(HttpStatusCode.Forbidden, AICompletionFailureKind.Authentication)]
        [DataRow(HttpStatusCode.BadRequest, AICompletionFailureKind.Rejected)]
        [DataRow(HttpStatusCode.NotFound, AICompletionFailureKind.Rejected)]
        public async Task CompleteAsync_ClassifiesHttpFailures(HttpStatusCode status, AICompletionFailureKind expected)
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(status, "{}"));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.FailureKind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }

        [TestMethod]
        public async Task CompleteAsync_ClassifiesClientSideTimeoutAsTimeout()
        {
            RecordingHandler handler = RecordingHandler.Throwing(new TaskCanceledException("timed out"));
            OpenAICompletionClient client = CreateClient(handler);

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AICompletionFailureKind.Timeout, result.FailureKind);
        }

        [TestMethod]
        public async Task CompleteAsync_ClassifiesNetworkErrorsAsTransientAndRedactsTheKey()
        {
            RecordingHandler handler = RecordingHandler.Throwing(new HttpRequestException("connection refused for " + ApiKey));
            OpenAICompletionClient client = CreateClient(handler);

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.AreEqual(AICompletionFailureKind.Transient, result.FailureKind);
            Assert.DoesNotContain(ApiKey, result.ErrorMessage);
        }

        [TestMethod]
        public async Task CompleteAsync_PropagatesCallerCancellation()
        {
            using CancellationTokenSource source = new();
            await source.CancelAsync();
            RecordingHandler handler = RecordingHandler.Throwing(new OperationCanceledException(source.Token));
            OpenAICompletionClient client = CreateClient(handler);

            await Assert.ThrowsAsync<OperationCanceledException>(() => client.CompleteAsync(CreateRequest(), source.Token));
        }

        [TestMethod]
        public async Task CompleteAsync_NeverReturnsTheApiKeyInErrorsOrRawBody()
        {
            string body = "{\"error\":{\"message\":\"Incorrect API key provided: " + ApiKey + "\"}}";
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.BadRequest, body));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.DoesNotContain(ApiKey, result.ErrorMessage);
            Assert.DoesNotContain(ApiKey, result.RawBody);
            Assert.Contains("***", result.ErrorMessage);
        }

        [TestMethod]
        public async Task CompleteAsync_NeverReturnsTheApiKeyWhenAuthenticationFails()
        {
            string body = "{\"error\":{\"message\":\"bad key " + ApiKey + "\"}}";
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.Unauthorized, body));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.AreEqual(AICompletionFailureKind.Authentication, result.FailureKind);
            Assert.DoesNotContain(ApiKey, result.ErrorMessage);
            Assert.DoesNotContain(ApiKey, result.RawBody);
        }

        [TestMethod]
        public async Task CompleteAsync_RejectsSuccessResponseWithoutChoices()
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.OK, "{\"choices\":[]}"));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AICompletionFailureKind.Rejected, result.FailureKind);
        }

        [TestMethod]
        public async Task CompleteAsync_RejectsSuccessResponseThatIsNotJson()
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.OK, "<html>gateway</html>"));

            AICompletionResult result = await client.CompleteAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(AICompletionFailureKind.Rejected, result.FailureKind);
            Assert.AreEqual("<html>gateway</html>", result.RawBody);
        }

        [TestMethod]
        public async Task CompleteAsync_ThrowsForNullRequest()
        {
            OpenAICompletionClient client = CreateClient(RecordingHandler.Responding(HttpStatusCode.OK, "{}"));

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.CompleteAsync(null, TestContext.CancellationToken));
        }

        private static string SuccessBody(string content)
        {
            return JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content } } },
                usage = new { prompt_tokens = 11, completion_tokens = 7, total_tokens = 18 }
            });
        }

        private static AICompletionRequest CreateRequest()
        {
            return new AICompletionRequest
            {
                SystemPrompt = "system prompt",
                UserPrompt = "user prompt",
                Settings = new ReviewEngineSettings
                {
                    Provider = AIProvider.OpenAI,
                    Endpoint = "https://review.test/v1/",
                    Model = "review-model",
                    MaxTokens = 1000,
                    SecretKeyName = "REVIEW_KEY",
                    Enabled = true
                }
            };
        }

        private static OpenAICompletionClient CreateClient(RecordingHandler handler, string secret = ApiKey)
        {
            return new OpenAICompletionClient(new StubSecretsProvider(secret), new HttpClient(handler), new NullDiagnostics(), new KeyedConcurrencyLimiter());
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            private readonly Exception _exception;

            private RecordingHandler(HttpStatusCode status, string body, Exception exception)
            {
                _status = status;
                _body = body;
                _exception = exception;
            }

            public int? RetryAfterSeconds { get; set; }

            public List<string> RequestUris { get; } = [];

            public List<string> RequestBodies { get; } = [];

            public List<string> AuthorizationHeaders { get; } = [];

            public static RecordingHandler Responding(HttpStatusCode status, string body)
            {
                return new RecordingHandler(status, body, null);
            }

            public static RecordingHandler Throwing(Exception exception)
            {
                return new RecordingHandler(HttpStatusCode.OK, string.Empty, exception);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUris.Add(request.RequestUri.ToString());
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

                if (_exception is not null)
                {
                    throw _exception;
                }

                HttpResponseMessage response = new(_status)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json")
                };

                if (RetryAfterSeconds.HasValue)
                {
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(RetryAfterSeconds.Value));
                }

                return response;
            }
        }

        private sealed class StubSecretsProvider : ISecretsProvider
        {
            private readonly string _secret;

            public StubSecretsProvider(string secret)
            {
                _secret = secret;
            }

            public Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_secret);
            }

            public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NullDiagnostics : IOperationalDiagnostics
        {
            public void Log(
                OperationalLogLevel level,
                string component,
                string operation,
                string message,
                Exception exception = null,
                TimeSpan? duration = null,
                int? retryAttempt = null)
            {
            }
        }
    }
}
