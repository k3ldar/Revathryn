using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueValidationRepositoryTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TryAddAsync_PersistsOriginalFindingSnapshotAndPendingStatus()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId, FindingSeverity.High), TestContext.CancellationToken);

            IssueValidation stored = await database.Validations.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNotNull(stored);
            Assert.AreEqual(findingId, stored.FindingId);
            Assert.AreEqual(database.RepositoryId, stored.RepositoryId);
            Assert.AreEqual(IssueValidationStatus.Pending, stored.Status);
            Assert.AreEqual(FindingSeverity.High, stored.OriginalSeverity);
            Assert.AreEqual("Original title", stored.OriginalTitle);
            Assert.AreEqual("Original description", stored.OriginalDescription);
            Assert.IsNull(stored.Verdict);
            Assert.AreEqual(0, stored.AttemptCount);
        }

        [TestMethod]
        public async Task TryAddAsync_ReturnsZeroWhenFindingAlreadyHasActiveValidation()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long first = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);
            long second = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            Assert.IsGreaterThan(0, first);
            Assert.AreEqual(0L, second);
            Assert.HasCount(1, await database.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task TryAddAsync_AllowsMultipleValidationsOncePreviousIsFinished()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long first = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Agreed, "First summary");
            long second = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Disagreed, "Second summary");

            IReadOnlyList<IssueValidation> history = await database.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken);

            Assert.AreNotEqual(first, second);
            Assert.HasCount(2, history);
            Assert.AreEqual(second, history[0].Id);
            Assert.AreEqual(first, history[1].Id);
        }

        [TestMethod]
        public async Task CompleteAsync_LeavesEarlierValidationsUnchanged()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            long first = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Agreed, "First summary");
            IssueValidation before = await database.Validations.GetByIdAsync(first, TestContext.CancellationToken);

            _ = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Disagreed, "Second summary");

            IssueValidation after = await database.Validations.GetByIdAsync(first, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationVerdict.Agreed, after.Verdict);
            Assert.AreEqual("First summary", after.Summary);
            Assert.AreEqual(before.Reasoning, after.Reasoning);
            Assert.AreEqual(before.SystemPrompt, after.SystemPrompt);
            Assert.AreEqual(before.Model, after.Model);
            Assert.AreEqual(before.CompletedUtc, after.CompletedUtc);
        }

        [TestMethod]
        public async Task SaveExecutionSnapshotAsync_PersistsPromptsModelAndContextReference()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            IssueValidation snapshot = new()
            {
                Id = id,
                Provider = AIProvider.OpenAI,
                Model = "review-model",
                EngineConfigSnapshotJson = "{\"model\":\"review-model\"}",
                PromptVersion = "v1",
                SystemPrompt = "system prompt",
                UserPrompt = "user prompt",
                ContextReferenceJson = "{\"mode\":1}"
            };

            await database.Validations.SaveExecutionSnapshotAsync(snapshot, TestContext.CancellationToken);

            IssueValidation stored = await database.Validations.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.AreEqual(AIProvider.OpenAI, stored.Provider);
            Assert.AreEqual("review-model", stored.Model);
            Assert.AreEqual("v1", stored.PromptVersion);
            Assert.AreEqual("system prompt", stored.SystemPrompt);
            Assert.AreEqual("user prompt", stored.UserPrompt);
            Assert.AreEqual("{\"mode\":1}", stored.ContextReferenceJson);
            Assert.AreEqual("{\"model\":\"review-model\"}", stored.EngineConfigSnapshotJson);
        }

        [TestMethod]
        public async Task CompleteAsync_PersistsVerdictConfidenceSeverityAndStructuredLists()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId, FindingSeverity.High), TestContext.CancellationToken);

            await database.Validations.CompleteAsync(new IssueValidation
            {
                Id = id,
                CompletedUtc = DateTime.UtcNow,
                Verdict = IssueValidationVerdict.PartiallyAgreed,
                Confidence = 0.91,
                Summary = "Valid but overstated",
                Reasoning = "Narrower path",
                RecommendedSeverity = FindingSeverity.Medium,
                SeverityReasoning = "Limited path",
                EvidenceJson = "[{\"file\":\"src/A.cs\"}]",
                SuggestionsJson = "[\"Narrow it\"]",
                LimitationsJson = "[\"No callers\"]",
                RawResponse = "{raw}",
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }, TestContext.CancellationToken);

            IssueValidation stored = await database.Validations.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Completed, stored.Status);
            Assert.AreEqual(IssueValidationVerdict.PartiallyAgreed, stored.Verdict);
            Assert.AreEqual(0.91, stored.Confidence);
            Assert.AreEqual(FindingSeverity.High, stored.OriginalSeverity);
            Assert.AreEqual(FindingSeverity.Medium, stored.RecommendedSeverity);
            Assert.AreEqual("[{\"file\":\"src/A.cs\"}]", stored.EvidenceJson);
            Assert.AreEqual("[\"Narrow it\"]", stored.SuggestionsJson);
            Assert.AreEqual("[\"No callers\"]", stored.LimitationsJson);
            Assert.AreEqual("{raw}", stored.RawResponse);
            Assert.AreEqual(15, stored.TotalTokens);
        }

        [TestMethod]
        public async Task CompleteAsync_AllowsMissingConfidence()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            await database.Validations.CompleteAsync(new IssueValidation
            {
                Id = id,
                CompletedUtc = DateTime.UtcNow,
                Verdict = IssueValidationVerdict.Inconclusive,
                Summary = "Unsure"
            }, TestContext.CancellationToken);

            IssueValidation stored = await database.Validations.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.IsNull(stored.Confidence);
            Assert.IsNull(stored.RecommendedSeverity);
            Assert.AreEqual(IssueValidationVerdict.Inconclusive, stored.Verdict);
        }

        [TestMethod]
        public async Task FailAsync_MarksFailedWithoutVerdictAndKeepsDiagnostics()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            await database.Validations.FailAsync(
                id,
                "provider exploded",
                FailureCategory.Permanent,
                "raw body",
                new TokenUsage { PromptTokens = 4, CompletionTokens = 0, TotalTokens = 4 },
                DateTime.UtcNow,
                TestContext.CancellationToken);

            IssueValidation stored = await database.Validations.GetByIdAsync(id, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, stored.Status);
            Assert.IsNull(stored.Verdict);
            Assert.AreEqual("provider exploded", stored.ErrorMessage);
            Assert.AreEqual(FailureCategory.Permanent, stored.FailureClassification);
            Assert.AreEqual("raw body", stored.RawResponse);
            Assert.AreEqual(4, stored.TotalTokens);
        }

        [TestMethod]
        public async Task FailAsync_AllowsANewValidationForTheSameFinding()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long failed = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            await database.Validations.FailAsync(failed, "boom", FailureCategory.Permanent, string.Empty, null, DateTime.UtcNow, TestContext.CancellationToken);

            IssueValidation retry = NewValidation(database, findingId);
            retry.RetryOfValidationId = failed;
            long retryId = await database.Validations.TryAddAsync(retry, TestContext.CancellationToken);

            Assert.IsGreaterThan(0, retryId);
            IssueValidation stored = await database.Validations.GetByIdAsync(retryId, TestContext.CancellationToken);
            Assert.AreEqual(failed, stored.RetryOfValidationId);
            Assert.HasCount(2, await database.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task GetActiveByFindingAsync_ReturnsOnlyPendingOrRunning()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            Assert.IsNull(await database.Validations.GetActiveByFindingAsync(findingId, TestContext.CancellationToken));

            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);
            IssueValidation active = await database.Validations.GetActiveByFindingAsync(findingId, TestContext.CancellationToken);
            Assert.AreEqual(id, active.Id);

            await database.Validations.FailAsync(id, "boom", FailureCategory.Permanent, string.Empty, null, DateTime.UtcNow, TestContext.CancellationToken);
            Assert.IsNull(await database.Validations.GetActiveByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ClaimNextAsync_ClaimsPendingValidationOnceAndIncrementsAttempts()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            DateTime now = DateTime.UtcNow;
            IssueValidation claimed = await database.Validations.ClaimNextAsync(now, TimeSpan.FromMinutes(5), TestContext.CancellationToken);
            IssueValidation second = await database.Validations.ClaimNextAsync(now, TimeSpan.FromMinutes(5), TestContext.CancellationToken);

            Assert.IsNotNull(claimed);
            Assert.AreEqual(id, claimed.Id);
            Assert.AreEqual(IssueValidationStatus.Running, claimed.Status);
            Assert.AreEqual(1, claimed.AttemptCount);
            Assert.IsNotNull(claimed.StartedUtc);
            Assert.IsNull(second);
        }

        [TestMethod]
        public async Task ClaimNextAsync_ReturnsNullWhenNothingIsQueued()
        {
            using ValidationTestDatabase database = new();

            IssueValidation claimed = await database.Validations.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(5), TestContext.CancellationToken);

            Assert.IsNull(claimed);
        }

        [TestMethod]
        public async Task ClaimNextAsync_ReclaimsValidationWhoseLeaseExpired()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            DateTime now = DateTime.UtcNow;
            _ = await database.Validations.ClaimNextAsync(now, TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            IssueValidation reclaimed = await database.Validations.ClaimNextAsync(now.AddMinutes(2), TimeSpan.FromMinutes(1), TestContext.CancellationToken);

            Assert.IsNotNull(reclaimed);
            Assert.AreEqual(id, reclaimed.Id);
            Assert.AreEqual(2, reclaimed.AttemptCount);
        }

        [TestMethod]
        public async Task ReleaseForRetryAsync_DelaysNextClaimUntilDue()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            DateTime now = DateTime.UtcNow;
            _ = await database.Validations.ClaimNextAsync(now, TimeSpan.FromMinutes(5), TestContext.CancellationToken);

            await database.Validations.ReleaseForRetryAsync(id, now.AddMinutes(10), "rate limited", "raw", new TokenUsage(), TestContext.CancellationToken);

            IssueValidation early = await database.Validations.ClaimNextAsync(now.AddMinutes(1), TimeSpan.FromMinutes(5), TestContext.CancellationToken);
            IssueValidation due = await database.Validations.ClaimNextAsync(now.AddMinutes(11), TimeSpan.FromMinutes(5), TestContext.CancellationToken);

            Assert.IsNull(early);
            Assert.IsNotNull(due);
            Assert.AreEqual(id, due.Id);
            Assert.AreEqual("rate limited", due.ErrorMessage);
            Assert.AreEqual(2, due.AttemptCount);
        }

        [TestMethod]
        public async Task GetLatestCompletedByFindingsAsync_IgnoresFailedAndReturnsNewestCompleted()
        {
            using ValidationTestDatabase database = new();
            long findingA = await database.AddFindingAsync(TestContext.CancellationToken, "fp-a");
            long findingB = await database.AddFindingAsync(TestContext.CancellationToken, "fp-b");

            _ = await CreateCompletedAsync(database, findingA, IssueValidationVerdict.Agreed, "Old");
            long newest = await CreateCompletedAsync(database, findingA, IssueValidationVerdict.Disagreed, "New");

            long failed = await database.Validations.TryAddAsync(NewValidation(database, findingB), TestContext.CancellationToken);
            await database.Validations.FailAsync(failed, "boom", FailureCategory.Permanent, string.Empty, null, DateTime.UtcNow, TestContext.CancellationToken);

            IReadOnlyDictionary<long, IssueValidation> latest = await database.Validations.GetLatestCompletedByFindingsAsync([findingA, findingB], TestContext.CancellationToken);

            Assert.HasCount(1, latest);
            Assert.AreEqual(newest, latest[findingA].Id);
            Assert.AreEqual(IssueValidationVerdict.Disagreed, latest[findingA].Verdict);
            Assert.IsFalse(latest.ContainsKey(findingB));
        }

        [TestMethod]
        public async Task GetLatestCompletedByFindingsAsync_ReturnsEmptyForEmptyInput()
        {
            using ValidationTestDatabase database = new();

            IReadOnlyDictionary<long, IssueValidation> latest = await database.Validations.GetLatestCompletedByFindingsAsync([], TestContext.CancellationToken);

            Assert.IsEmpty(latest);
        }

        [TestMethod]
        public async Task GetLatestAsync_ReturnsNewestRegardlessOfStatus()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            _ = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Agreed, "Done");
            long pending = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            IssueValidation latest = await database.Validations.GetLatestAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(pending, latest.Id);
        }

        [TestMethod]
        public async Task GetByIdAsync_ReturnsNullForUnknownValidation()
        {
            using ValidationTestDatabase database = new();

            Assert.IsNull(await database.Validations.GetByIdAsync(999, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task TryAddAsync_ThrowsForNullValidation()
        {
            using ValidationTestDatabase database = new();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => database.Validations.TryAddAsync(null, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ValidationsDoNotChangeTheOriginalFinding()
        {
            using ValidationTestDatabase database = new();
            long findingId = await database.AddFindingAsync(TestContext.CancellationToken);

            Finding before = await database.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            _ = await CreateCompletedAsync(database, findingId, IssueValidationVerdict.Disagreed, "Not an issue");
            Finding after = await database.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(before.Title, after.Title);
            Assert.AreEqual(before.Description, after.Description);
            Assert.AreEqual(before.Severity, after.Severity);
            Assert.AreEqual(before.Status, after.Status);
        }

        [TestMethod]
        public async Task ReviewEngineSettingsRepository_ReturnsNullWhenNotConfigured()
        {
            using ValidationTestDatabase database = new();

            Assert.IsNull(await database.EngineSettings.GetAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ReviewEngineSettingsRepository_RoundTripsAndStoresSecretNameOnly()
        {
            using ValidationTestDatabase database = new();

            await database.EngineSettings.SaveAsync(new ReviewEngineSettings
            {
                Provider = AIProvider.OpenAI,
                Endpoint = "https://example.test/v1",
                Model = "big-model",
                Temperature = 0.2,
                ReasoningEffort = "high",
                MaxTokens = 4096,
                MaxSimultaneousConnections = 8,
                SecretKeyName = "REVIEW_KEY",
                Enabled = true,
                AdditionalOptions = "{\"top_p\":0.5}",
                LastModifiedUtc = DateTime.UtcNow
            }, TestContext.CancellationToken);

            ReviewEngineSettings stored = await database.EngineSettings.GetAsync(TestContext.CancellationToken);

            Assert.IsNotNull(stored);
            Assert.AreEqual(AIProvider.OpenAI, stored.Provider);
            Assert.AreEqual("big-model", stored.Model);
            Assert.AreEqual(0.2, stored.Temperature);
            Assert.AreEqual("high", stored.ReasoningEffort);
            Assert.AreEqual(4096, stored.MaxTokens);
            Assert.AreEqual(8, stored.MaxSimultaneousConnections);
            Assert.AreEqual("REVIEW_KEY", stored.SecretKeyName);
            Assert.IsTrue(stored.Enabled);
            Assert.AreEqual("{\"top_p\":0.5}", stored.AdditionalOptions);
        }

        [TestMethod]
        public async Task ReviewEngineSettingsRepository_SaveReplacesExistingSettings()
        {
            using ValidationTestDatabase database = new();

            await database.EngineSettings.SaveAsync(new ReviewEngineSettings { Model = "first", MaxTokens = 100, MaxSimultaneousConnections = 2, LastModifiedUtc = DateTime.UtcNow }, TestContext.CancellationToken);
            await database.EngineSettings.SaveAsync(new ReviewEngineSettings { Model = "second", MaxTokens = 200, MaxSimultaneousConnections = 6, LastModifiedUtc = DateTime.UtcNow }, TestContext.CancellationToken);

            ReviewEngineSettings stored = await database.EngineSettings.GetAsync(TestContext.CancellationToken);

            Assert.AreEqual("second", stored.Model);
            Assert.AreEqual(200, stored.MaxTokens);
            Assert.AreEqual(6, stored.MaxSimultaneousConnections);
            Assert.IsNull(stored.Temperature);
        }

        private static IssueValidation NewValidation(ValidationTestDatabase database, long findingId, FindingSeverity severity = FindingSeverity.Medium)
        {
            return new IssueValidation
            {
                FindingId = findingId,
                RepositoryId = database.RepositoryId,
                CreatedUtc = DateTime.UtcNow,
                Provider = AIProvider.OpenAI,
                Model = "review-model",
                OriginalSeverity = severity,
                OriginalTitle = "Original title",
                OriginalDescription = "Original description",
                OriginalSuggestedFix = "Original fix"
            };
        }

        private async Task<long> CreateCompletedAsync(ValidationTestDatabase database, long findingId, IssueValidationVerdict verdict, string summary)
        {
            long id = await database.Validations.TryAddAsync(NewValidation(database, findingId), TestContext.CancellationToken);

            await database.Validations.SaveExecutionSnapshotAsync(new IssueValidation
            {
                Id = id,
                Provider = AIProvider.OpenAI,
                Model = "review-model",
                SystemPrompt = $"system for {summary}",
                UserPrompt = $"user for {summary}"
            }, TestContext.CancellationToken);

            await database.Validations.CompleteAsync(new IssueValidation
            {
                Id = id,
                CompletedUtc = DateTime.UtcNow,
                Verdict = verdict,
                Summary = summary,
                Reasoning = $"reasoning for {summary}"
            }, TestContext.CancellationToken);

            return id;
        }

        private sealed class ValidationTestDatabase : IDisposable
        {
            private readonly string _directory;

            public ValidationTestDatabase()
            {
                _directory = Path.Combine(Path.GetTempPath(), "revathryn-data-tests", Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(_directory);

                string databasePath = Path.Combine(_directory, "test.db");
                DatabaseOptions options = new(new RevathrynSettings("test")) { ConnectionString = $"Data Source={databasePath};Pooling=False" };
                SqliteConnectionFactory factory = new(options);

                SqliteDatabaseInitializer initializer = new(factory);
                initializer.Initialize();

                Validations = new IssueValidationRepository(factory);
                EngineSettings = new ReviewEngineSettingsRepository(factory);
                Findings = new FindingRepository(factory);

                RepositoryRepository repositories = new(factory);
                DateTime now = DateTime.UtcNow;
                RepositoryId = repositories
                    .AddAsync(new Repository { Name = "Repo", LocalPath = "C:\\src\\Repo", Enabled = true, CreatedDate = now, LastModifiedDate = now })
                    .GetAwaiter()
                    .GetResult();
            }

            public long RepositoryId { get; }

            public IssueValidationRepository Validations { get; }

            public ReviewEngineSettingsRepository EngineSettings { get; }

            public FindingRepository Findings { get; }

            public async Task<long> AddFindingAsync(CancellationToken cancellationToken, string fingerprint = "fp-1")
            {
                DateTime now = DateTime.UtcNow;

                return await Findings.AddAsync(new Finding
                {
                    RepositoryId = RepositoryId,
                    Fingerprint = fingerprint,
                    Severity = FindingSeverity.High,
                    Title = "Original title",
                    Description = "Original description",
                    Status = FindingStatus.New,
                    Category = FindingCategory.CodeIssue,
                    CreatedDate = now,
                    LastModifiedDate = now
                }, cancellationToken);
            }

            public void Dispose()
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, true);
                }
            }
        }
    }
}
