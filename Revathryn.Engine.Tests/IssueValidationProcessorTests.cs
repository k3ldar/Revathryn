using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueValidationProcessorTests
    {
        private const string OriginalContent = "line one\nline two\nline three";
        private const string OriginalSha = "sha-original";
        private const string RawResponse = "{\"raw\":true}";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task ProcessNextAsync_ReturnsFalseWhenNothingIsQueued()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            ScriptedValidationEngine engine = new();

            bool processed = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            Assert.IsFalse(processed);
            Assert.HasCount(0, engine.Requests);
        }

        [TestMethod]
        [DataRow(IssueValidationVerdict.Agreed)]
        [DataRow(IssueValidationVerdict.Disagreed)]
        [DataRow(IssueValidationVerdict.PartiallyAgreed)]
        [DataRow(IssueValidationVerdict.Inconclusive)]
        public async Task ProcessNextAsync_PersistsEachVerdictAsCompleted(IssueValidationVerdict verdict)
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateSuccess(verdict));

            bool processed = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.IsTrue(processed);
            Assert.AreEqual(IssueValidationStatus.Completed, stored.Status);
            Assert.AreEqual(verdict, stored.Verdict);
            Assert.IsNull(stored.FailureClassification);
        }

        [TestMethod]
        public async Task ProcessNextAsync_PersistsFullResultAndExecutionSnapshot()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateSuccess(IssueValidationVerdict.PartiallyAgreed));

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(0.91, stored.Confidence);
            Assert.AreEqual("Valid but overstated.", stored.Summary);
            Assert.AreEqual("Narrower path than reported.", stored.Reasoning);
            Assert.AreEqual(FindingSeverity.High, stored.OriginalSeverity);
            Assert.AreEqual(FindingSeverity.Medium, stored.RecommendedSeverity);
            Assert.AreEqual("Limited path.", stored.SeverityReasoning);
            Assert.AreEqual(RawResponse, stored.RawResponse);
            Assert.AreEqual(AIProvider.OpenAI, stored.Provider);
            Assert.AreEqual("review-model", stored.Model);
            Assert.AreEqual(IssueValidationPrompts.Version, stored.PromptVersion);
            Assert.AreEqual(IssueValidationPrompts.SystemPrompt, stored.SystemPrompt);
            Assert.AreEqual(30, stored.TotalTokens);
            Assert.Contains("review-model", stored.EngineConfigSnapshotJson);
            Assert.Contains("src/Example.cs", stored.EvidenceJson);
            Assert.Contains("The calling code was not available.", stored.LimitationsJson);
            Assert.Contains("Narrow the original issue description.", stored.SuggestionsJson);
            Assert.IsNotNull(stored.CompletedUtc);
        }

        [TestMethod]
        public async Task ProcessNextAsync_SuppliesDetectionContextOriginalPromptsAndIndependentInstructions()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateSuccess(IssueValidationVerdict.Agreed));

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            Assert.HasCount(1, engine.Requests);
            IssueValidationRequest request = engine.Requests[0];
            Assert.AreEqual("review-model", request.Settings.Model);
            Assert.AreEqual(IssueValidationPrompts.SystemPrompt, request.SystemPrompt);
            Assert.Contains("Do not agree simply because it was reported", request.SystemPrompt);
            Assert.Contains("Potential missing null check", request.UserPrompt);
            Assert.Contains("ORIGINAL-SYSTEM-PROMPT", request.UserPrompt);
            Assert.Contains("ORIGINAL-REVIEW-PROMPT", request.UserPrompt);
            Assert.Contains("1: line one", request.UserPrompt);
            Assert.AreEqual(3, request.SuppliedSources[ValidationTestFixture.SourcePath]);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(request.UserPrompt, stored.UserPrompt);
            Assert.Contains("OriginalSnapshot", stored.ContextReferenceJson);
        }

        [TestMethod]
        public async Task ProcessNextAsync_CompletedSnapshotIsUnaffectedByLaterConfigurationChange()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateSuccess(IssueValidationVerdict.Agreed));
            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            await fixture.Settings.SaveAsync(
                new ReviewEngineSettings { Provider = AIProvider.OpenAI, Model = "different-model", Enabled = true, MaxTokens = 10, SecretKeyName = "K", LastModifiedUtc = DateTime.UtcNow },
                TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual("review-model", stored.Model);
            Assert.Contains("review-model", stored.EngineConfigSnapshotJson);
            Assert.DoesNotContain("different-model", stored.EngineConfigSnapshotJson);
        }

        [TestMethod]
        public async Task ProcessNextAsync_TimeoutIsRetriedThenCompletes()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine()
                .Returns(CreateFailure(AICompletionFailureKind.Timeout, "timed out"))
                .Returns(CreateSuccess(IssueValidationVerdict.Agreed));
            IssueValidationProcessor processor = CreateProcessor(fixture, engine);

            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            IssueValidation afterTimeout = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            IssueValidation afterRetry = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationStatus.Pending, afterTimeout.Status);
            Assert.AreEqual("timed out", afterTimeout.ErrorMessage);
            Assert.AreEqual(IssueValidationStatus.Completed, afterRetry.Status);
            Assert.AreEqual(IssueValidationVerdict.Agreed, afterRetry.Verdict);
            Assert.AreEqual(2, afterRetry.AttemptCount);
        }

        [TestMethod]
        public async Task ProcessNextAsync_RateLimitHonoursRetryAfter()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            IssueValidationEngineResult limited = CreateFailure(AICompletionFailureKind.RateLimited, "rate limited");
            limited.RetryAfter = TimeSpan.FromMinutes(30);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(limited);
            IssueValidationProcessor processor = CreateProcessor(fixture, engine);

            DateTime before = DateTime.UtcNow;
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            bool processedAgain = await processor.ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.IsFalse(processedAgain);
            Assert.AreEqual(IssueValidationStatus.Pending, stored.Status);
            Assert.IsNotNull(stored.NextAttemptUtc);
            Assert.IsGreaterThanOrEqualTo(before.AddMinutes(29), stored.NextAttemptUtc.Value);
            Assert.HasCount(1, engine.Requests);
        }

        [TestMethod]
        [DataRow(AICompletionFailureKind.Timeout)]
        [DataRow(AICompletionFailureKind.RateLimited)]
        [DataRow(AICompletionFailureKind.Transient)]
        public async Task ProcessNextAsync_RetryableFailureEndsAsFailedTransientAfterAttemptLimit(AICompletionFailureKind kind)
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateFailure(kind, "provider unavailable"));
            IssueValidationProcessor processor = CreateProcessor(fixture, engine);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!await processor.ProcessNextAsync(TestContext.CancellationToken))
                {
                    break;
                }
            }

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.Transient, stored.FailureClassification);
            Assert.IsNull(stored.Verdict);
            Assert.AreEqual(new ResilienceOptions().GetRetryPolicy(RetryOperationType.ExternalService).MaxAttempts, stored.AttemptCount);
        }

        [TestMethod]
        public async Task ProcessNextAsync_PermanentProviderFailureIsFailedNotInconclusive()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateFailure(AICompletionFailureKind.Rejected, "content rejected"));

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.Permanent, stored.FailureClassification);
            Assert.IsNull(stored.Verdict);
            Assert.AreEqual("content rejected", stored.ErrorMessage);
            Assert.HasCount(1, engine.Requests);
        }

        [TestMethod]
        [DataRow(AICompletionFailureKind.Authentication)]
        [DataRow(AICompletionFailureKind.Configuration)]
        public async Task ProcessNextAsync_AuthenticationAndConfigurationFailuresAreClassifiedAsConfiguration(AICompletionFailureKind kind)
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateFailure(kind, "bad key"));

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.Configuration, stored.FailureClassification);
            Assert.HasCount(1, engine.Requests);
        }

        [TestMethod]
        public async Task ProcessNextAsync_MalformedOutputFailsAsDataIntegrityAndPreservesRawResponse()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            IssueValidationEngineResult malformed = CreateFailure(AICompletionFailureKind.None, "malformed");
            malformed.Malformed = true;
            malformed.RawResponse = "not json at all";
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(malformed);

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.DataIntegrity, stored.FailureClassification);
            Assert.AreEqual("not json at all", stored.RawResponse);
            Assert.IsNull(stored.Verdict);
        }

        [TestMethod]
        public async Task ProcessNextAsync_FailureNeverChangesTheOriginalFinding()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            Finding before = await fixture.Findings.GetByIdAsync(await FindingIdAsync(fixture, validationId), TestContext.CancellationToken);
            ScriptedValidationEngine engine = new ScriptedValidationEngine().Returns(CreateFailure(AICompletionFailureKind.Rejected, "nope"));

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            Finding after = await fixture.Findings.GetByIdAsync(before.Id, TestContext.CancellationToken);
            Assert.AreEqual(before.Title, after.Title);
            Assert.AreEqual(before.Description, after.Description);
            Assert.AreEqual(before.Severity, after.Severity);
            Assert.AreEqual(before.Status, after.Status);
            Assert.AreEqual(before.LastModifiedDate, after.LastModifiedDate);
        }

        [TestMethod]
        public async Task ProcessNextAsync_LaterFailureDoesNotAlterEarlierCompletedValidation()
        {
            using ValidationTestFixture fixture = new();
            long firstId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine()
                .Returns(CreateSuccess(IssueValidationVerdict.Agreed))
                .Returns(CreateFailure(AICompletionFailureKind.Rejected, "later failure"));
            IssueValidationProcessor processor = CreateProcessor(fixture, engine);
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            IssueValidation firstBefore = await fixture.Validations.GetByIdAsync(firstId, TestContext.CancellationToken);

            long findingId = firstBefore.FindingId;
            long secondId = await fixture.Validations.TryAddAsync(NewPending(firstBefore), TestContext.CancellationToken);
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation firstAfter = await fixture.Validations.GetByIdAsync(firstId, TestContext.CancellationToken);
            IssueValidation second = await fixture.Validations.GetByIdAsync(secondId, TestContext.CancellationToken);
            IReadOnlyList<IssueValidation> all = await fixture.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken);
            Assert.HasCount(2, all);
            Assert.AreEqual(IssueValidationStatus.Completed, firstAfter.Status);
            Assert.AreEqual(IssueValidationVerdict.Agreed, firstAfter.Verdict);
            Assert.AreEqual(firstBefore.Summary, firstAfter.Summary);
            Assert.AreEqual(firstBefore.CompletedUtc, firstAfter.CompletedUtc);
            Assert.AreEqual(IssueValidationStatus.Failed, second.Status);
        }

        [TestMethod]
        public async Task ProcessNextAsync_FailsAsConfigurationWhenReviewEngineSettingsAreMissing()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture, saveSettings: false);
            ScriptedValidationEngine engine = new();

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.Configuration, stored.FailureClassification);
            Assert.AreEqual(Strings.ReviewEngineUnavailableAtExecution, stored.ErrorMessage);
            Assert.HasCount(0, engine.Requests);
        }

        [TestMethod]
        public async Task ProcessNextAsync_FailsAsConfigurationWhenReviewEngineIsDisabled()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture, saveSettings: false);
            await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken, enabled: false);
            ScriptedValidationEngine engine = new();

            _ = await CreateProcessor(fixture, engine).ProcessNextAsync(TestContext.CancellationToken);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.AreEqual(FailureCategory.Configuration, stored.FailureClassification);
            Assert.HasCount(0, engine.Requests);
        }

        [TestMethod]
        public async Task ProcessNextAsync_UnexpectedExceptionIsRetriedThenFailsWhenPolicyStops()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            ScriptedValidationEngine engine = new ScriptedValidationEngine()
                .Throws(new InvalidOperationException("boom"))
                .Throws(new InvalidOperationException("boom again"));
            TestRetryPolicy policy = new() { RetryAttempts = 1 };
            RecordingDiagnostics diagnostics = new();
            IssueValidationProcessor processor = CreateProcessor(fixture, engine, policy, diagnostics);

            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            IssueValidation afterFirst = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            IssueValidation afterSecond = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationStatus.Pending, afterFirst.Status);
            Assert.AreEqual("boom", afterFirst.ErrorMessage);
            Assert.AreEqual(IssueValidationStatus.Failed, afterSecond.Status);
            Assert.AreEqual(FailureCategory.Unexpected, afterSecond.FailureClassification);
            Assert.IsNull(afterSecond.Verdict);
            Assert.IsTrue(diagnostics.Messages.Exists(m => m == "boom"));
        }

        [TestMethod]
        public async Task ProcessNextAsync_CancellationReleasesTheClaimAndRethrows()
        {
            using ValidationTestFixture fixture = new();
            long validationId = await QueueAsync(fixture);
            using CancellationTokenSource source = new();
            IssueValidationProcessor processor = CreateProcessor(fixture, new CancellingEngine(source));

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ProcessNextAsync(source.Token));

            IssueValidation stored = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Pending, stored.Status);
            Assert.IsNull(stored.LeasedUntilUtc);
        }

        private static IssueValidationProcessor CreateProcessor(ValidationTestFixture fixture, IIssueValidationEngine engine, TestRetryPolicy policy = null, RecordingDiagnostics diagnostics = null)
        {
            IssueValidationContextBuilder builder = new(fixture.Repositories, fixture.Files, fixture.ReviewTypes, fixture.Contexts);

            return new IssueValidationProcessor(
                fixture.Validations,
                fixture.Findings,
                fixture.Settings,
                builder,
                engine,
                new TestRetryPolicyProvider(policy ?? new TestRetryPolicy()),
                new ResilienceOptions(),
                diagnostics ?? new RecordingDiagnostics());
        }

        private async Task<long> QueueAsync(ValidationTestFixture fixture, bool saveSettings = true)
        {
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);

            long contextId = await fixture.AddContextAsync(
                new AIReviewContext
                {
                    Sha256 = OriginalSha,
                    FilePath = ValidationTestFixture.SourcePath,
                    Language = "csharp",
                    FileContent = OriginalContent,
                    ReviewInstructions = "ORIGINAL-SYSTEM-PROMPT",
                    ReviewPrompt = "ORIGINAL-REVIEW-PROMPT",
                    ReviewDefinitionVersion = "7",
                    EngineName = "Detector",
                    EngineProvider = "OpenAI",
                    EngineModel = "detect-model",
                    RequestedContextKeys = []
                },
                TestContext.CancellationToken);

            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);
            Finding finding = await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);

            if (saveSettings)
            {
                await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken);
            }

            return await fixture.Validations.TryAddAsync(
                new IssueValidation
                {
                    FindingId = finding.Id,
                    RepositoryId = finding.RepositoryId,
                    Status = IssueValidationStatus.Pending,
                    CreatedUtc = DateTime.UtcNow,
                    OriginalSeverity = finding.Severity,
                    OriginalTitle = finding.Title,
                    OriginalDescription = finding.Description,
                    OriginalSuggestedFix = finding.SuggestedFix
                },
                TestContext.CancellationToken);
        }

        private async Task<long> FindingIdAsync(ValidationTestFixture fixture, long validationId)
        {
            IssueValidation validation = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            return validation.FindingId;
        }

        private static IssueValidation NewPending(IssueValidation template)
        {
            return new IssueValidation
            {
                FindingId = template.FindingId,
                RepositoryId = template.RepositoryId,
                Status = IssueValidationStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                OriginalSeverity = template.OriginalSeverity,
                OriginalTitle = template.OriginalTitle,
                OriginalDescription = template.OriginalDescription,
                OriginalSuggestedFix = template.OriginalSuggestedFix
            };
        }

        private static IssueValidationEngineResult CreateSuccess(IssueValidationVerdict verdict)
        {
            return new IssueValidationEngineResult
            {
                Success = true,
                Verdict = verdict,
                Confidence = 0.91,
                Summary = "Valid but overstated.",
                Reasoning = "Narrower path than reported.",
                RecommendedSeverity = FindingSeverity.Medium,
                SeverityReasoning = "Limited path.",
                Evidence = [new IssueValidationEvidence { File = ValidationTestFixture.SourcePath, LineStart = 1, LineEnd = 2, Explanation = "Accessed without a check." }],
                Suggestions = ["Narrow the original issue description."],
                Limitations = ["The calling code was not available."],
                RawResponse = RawResponse,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20, TotalTokens = 30 }
            };
        }

        private static IssueValidationEngineResult CreateFailure(AICompletionFailureKind kind, string message)
        {
            return new IssueValidationEngineResult
            {
                Success = false,
                FailureKind = kind,
                ErrorMessage = message
            };
        }

        private sealed class CancellingEngine : IIssueValidationEngine
        {
            private readonly CancellationTokenSource _source;

            public CancellingEngine(CancellationTokenSource source)
            {
                _source = source;
            }

            public Task<IssueValidationEngineResult> ValidateAsync(IssueValidationRequest request, CancellationToken cancellationToken = default)
            {
                _source.Cancel();
                throw new OperationCanceledException(_source.Token);
            }
        }

        private sealed class TestRetryPolicy : IRetryPolicy
        {
            public int RetryAttempts { get; set; } = int.MaxValue;

            public RetryDecision Evaluate(int attempt, Exception exception)
            {
                return attempt <= RetryAttempts
                    ? RetryDecision.Retry(TimeSpan.Zero, FailureCategory.Transient)
                    : RetryDecision.Stop(FailureCategory.Unexpected);
            }

            public TimeSpan GetRetryDelay(int attempt)
            {
                return TimeSpan.Zero;
            }
        }

        private sealed class TestRetryPolicyProvider : IRetryPolicyProvider
        {
            private readonly IRetryPolicy _policy;

            public TestRetryPolicyProvider(IRetryPolicy policy)
            {
                _policy = policy;
            }

            public IRetryPolicy GetPolicy(RetryOperationType operationType)
            {
                return _policy;
            }
        }
    }
}
