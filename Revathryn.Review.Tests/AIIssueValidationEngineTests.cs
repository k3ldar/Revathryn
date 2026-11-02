using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Review.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class AIIssueValidationEngineTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task ValidateAsync_PassesPromptsAndSettingsToTheCompletionClient()
        {
            FakeCompletionClient client = new(new AICompletionResult { Success = true, Content = "{\"verdict\":\"Agreed\",\"summary\":\"ok\"}" });
            AIIssueValidationEngine engine = new(client);
            IssueValidationRequest request = CreateRequest();

            _ = await engine.ValidateAsync(request, TestContext.CancellationToken);

            Assert.IsNotNull(client.LastRequest);
            Assert.AreSame(request.Settings, client.LastRequest.Settings);
            Assert.AreEqual("system", client.LastRequest.SystemPrompt);
            Assert.AreEqual("user", client.LastRequest.UserPrompt);
            Assert.IsTrue(client.LastRequest.RequireJson);
        }

        [TestMethod]
        public async Task ValidateAsync_ReturnsParsedVerdictAndUsage()
        {
            FakeCompletionClient client = new(new AICompletionResult
            {
                Success = true,
                Content = "{\"verdict\":\"Disagreed\",\"confidence\":0.8,\"summary\":\"No\"}",
                Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 3, TotalTokens = 8 }
            });

            IssueValidationEngineResult result = await new AIIssueValidationEngine(client).ValidateAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(IssueValidationVerdict.Disagreed, result.Verdict);
            Assert.AreEqual(0.8, result.Confidence);
            Assert.AreEqual(8, result.Usage.TotalTokens);
        }

        [TestMethod]
        public async Task ValidateAsync_ReportsMalformedOutputWithoutAVerdictAndKeepsUsage()
        {
            FakeCompletionClient client = new(new AICompletionResult
            {
                Success = true,
                Content = "I think it is fine.",
                Usage = new TokenUsage { TotalTokens = 9 }
            });

            IssueValidationEngineResult result = await new AIIssueValidationEngine(client).ValidateAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Malformed);
            Assert.AreEqual("I think it is fine.", result.RawResponse);
            Assert.AreEqual(9, result.Usage.TotalTokens);
        }

        [TestMethod]
        [DataRow(AICompletionFailureKind.Timeout)]
        [DataRow(AICompletionFailureKind.RateLimited)]
        [DataRow(AICompletionFailureKind.Transient)]
        [DataRow(AICompletionFailureKind.Authentication)]
        [DataRow(AICompletionFailureKind.Rejected)]
        [DataRow(AICompletionFailureKind.Configuration)]
        public async Task ValidateAsync_PropagatesProviderFailureWithoutMalformedFlag(AICompletionFailureKind kind)
        {
            FakeCompletionClient client = new(new AICompletionResult
            {
                Success = false,
                FailureKind = kind,
                ErrorMessage = "provider said no",
                RetryAfter = TimeSpan.FromSeconds(7),
                RawBody = "body"
            });

            IssueValidationEngineResult result = await new AIIssueValidationEngine(client).ValidateAsync(CreateRequest(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Malformed);
            Assert.AreEqual(kind, result.FailureKind);
            Assert.AreEqual("provider said no", result.ErrorMessage);
            Assert.AreEqual(TimeSpan.FromSeconds(7), result.RetryAfter);
            Assert.AreEqual("body", result.RawResponse);
        }

        [TestMethod]
        public async Task ValidateAsync_ThrowsForNullRequest()
        {
            AIIssueValidationEngine engine = new(new FakeCompletionClient(new AICompletionResult()));

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.ValidateAsync(null, TestContext.CancellationToken));
        }

        private static IssueValidationRequest CreateRequest()
        {
            return new IssueValidationRequest
            {
                Settings = new ReviewEngineSettings { Model = "review-model" },
                SystemPrompt = "system",
                UserPrompt = "user"
            };
        }

        private sealed class FakeCompletionClient : IAICompletionClient
        {
            private readonly AICompletionResult _result;

            public FakeCompletionClient(AICompletionResult result)
            {
                _result = result;
            }

            public AICompletionRequest LastRequest { get; private set; }

            public Task<AICompletionResult> CompleteAsync(AICompletionRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult(_result);
            }
        }
    }
}
