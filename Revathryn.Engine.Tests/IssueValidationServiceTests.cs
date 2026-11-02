using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Resources;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueValidationServiceTests
    {
        private const string SecretName = "REVIEW_KEY";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task RequestAsync_CreatesPendingValidationSnapshottingTheOriginalFinding()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);

            IssueValidationRequestResult result = await service.RequestAsync(findingId, TestContext.CancellationToken);

            Assert.IsTrue(result.Created);
            Assert.IsFalse(result.AlreadyActive);
            Assert.IsNull(result.Rejection);
            Assert.AreEqual(Strings.ValidationQueued, result.Message);

            IssueValidation stored = await fixture.Validations.GetByIdAsync(result.Validation.Id, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Pending, stored.Status);
            Assert.AreEqual(findingId, stored.FindingId);
            Assert.AreEqual(fixture.RepositoryId, stored.RepositoryId);
            Assert.AreEqual(FindingSeverity.High, stored.OriginalSeverity);
            Assert.AreEqual("Potential missing null check", stored.OriginalTitle);
            Assert.IsNull(stored.Verdict);
            Assert.IsNull(stored.RetryOfValidationId);
        }

        [TestMethod]
        public async Task RequestAsync_DoesNotModifyTheOriginalFinding()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            Finding before = await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);

            _ = await CreateService(fixture).RequestAsync(findingId, TestContext.CancellationToken);

            Finding after = await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            Assert.AreEqual(before.Severity, after.Severity);
            Assert.AreEqual(before.Status, after.Status);
            Assert.AreEqual(before.Title, after.Title);
            Assert.AreEqual(before.Description, after.Description);
            Assert.AreEqual(before.LastModifiedDate, after.LastModifiedDate);
        }

        [TestMethod]
        public async Task RequestAsync_SecondRequestWhileActiveReturnsTheSameValidation()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);

            IssueValidationRequestResult first = await service.RequestAsync(findingId, TestContext.CancellationToken);
            IssueValidationRequestResult second = await service.RequestAsync(findingId, TestContext.CancellationToken);

            Assert.IsTrue(first.Created);
            Assert.IsFalse(second.Created);
            Assert.IsTrue(second.AlreadyActive);
            Assert.AreEqual(first.Validation.Id, second.Validation.Id);
            Assert.HasCount(1, await fixture.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RequestAsync_RapidParallelRequestsCreateOnlyOneActiveValidation()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);

            IssueValidationRequestResult[] results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => service.RequestAsync(findingId, TestContext.CancellationToken)));

            Assert.ContainsSingle(r => r.Created, results);
            Assert.HasCount(1, await fixture.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
            Assert.IsTrue(results.All(r => r.Validation is not null));
        }

        [TestMethod]
        public async Task RequestAsync_NewValidationAllowedAfterPreviousCompleted()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);
            IssueValidationRequestResult first = await service.RequestAsync(findingId, TestContext.CancellationToken);
            await CompleteAsync(fixture, first.Validation.Id);

            IssueValidationRequestResult second = await service.RequestAsync(findingId, TestContext.CancellationToken);

            Assert.IsTrue(second.Created);
            Assert.AreNotEqual(first.Validation.Id, second.Validation.Id);
            Assert.HasCount(2, await service.GetByFindingAsync(findingId, TestContext.CancellationToken));
            Assert.AreEqual(IssueValidationVerdict.Agreed, (await service.GetByIdAsync(first.Validation.Id, TestContext.CancellationToken)).Verdict);
        }

        [TestMethod]
        public async Task RequestAsync_UnknownFindingIsRejected()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();

            IssueValidationRequestResult result = await CreateService(fixture).RequestAsync(9999, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.IssueNotFound, result.Rejection);
            Assert.IsFalse(result.Created);
            Assert.IsNull(result.Validation);
        }

        [TestMethod]
        public async Task RequestAsync_FindingOfMissingRepositoryIsRejected()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = new(
                fixture.Findings,
                new NoRepositories(),
                fixture.Validations,
                CreateConfiguration(fixture),
                new RecordingDiagnostics());

            IssueValidationRequestResult result = await service.RequestAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.RepositoryNotFound, result.Rejection);
        }

        [TestMethod]
        public async Task RequestAsync_NonCodeIssueIsRejected()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken, FindingCategory.OperationalFailure);

            IssueValidationRequestResult result = await CreateService(fixture).RequestAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.NotCodeIssue, result.Rejection);
            Assert.IsEmpty(await fixture.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RequestAsync_UnavailableEngineIsRejectedWithReason()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync("x", "sha", TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);

            IssueValidationRequestResult result = await CreateService(fixture).RequestAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.ReviewEngineUnavailable, result.Rejection);
            Assert.AreEqual(Strings.ReviewEngineNotConfigured, result.Message);
            Assert.IsEmpty(await fixture.Validations.GetByFindingAsync(findingId, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RetryAsync_FailedValidationCreatesNewRecordAndKeepsFailedOne()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);
            IssueValidationRequestResult first = await service.RequestAsync(findingId, TestContext.CancellationToken);
            await FailAsync(fixture, first.Validation.Id);

            IssueValidationRequestResult retry = await service.RetryAsync(first.Validation.Id, TestContext.CancellationToken);

            Assert.IsTrue(retry.Created);
            Assert.AreNotEqual(first.Validation.Id, retry.Validation.Id);
            Assert.AreEqual(first.Validation.Id, retry.Validation.RetryOfValidationId);

            IssueValidation original = await service.GetByIdAsync(first.Validation.Id, TestContext.CancellationToken);
            Assert.AreEqual(IssueValidationStatus.Failed, original.Status);
            Assert.IsNull(original.Verdict);
            Assert.AreEqual("provider down", original.ErrorMessage);
        }

        [TestMethod]
        public async Task RetryAsync_RejectsValidationThatHasNotFailed()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);
            IssueValidationRequestResult pending = await service.RequestAsync(findingId, TestContext.CancellationToken);

            IssueValidationRequestResult result = await service.RetryAsync(pending.Validation.Id, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.NotRetryable, result.Rejection);
        }

        [TestMethod]
        public async Task RetryAsync_RejectsCompletedValidation()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);
            IssueValidationRequestResult first = await service.RequestAsync(findingId, TestContext.CancellationToken);
            await CompleteAsync(fixture, first.Validation.Id);

            IssueValidationRequestResult result = await service.RetryAsync(first.Validation.Id, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.NotRetryable, result.Rejection);
        }

        [TestMethod]
        public async Task RetryAsync_UnknownValidationIsRejected()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();

            IssueValidationRequestResult result = await CreateService(fixture).RetryAsync(12345, TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationRejection.NotRetryable, result.Rejection);
        }

        [TestMethod]
        public async Task RetryAsync_WhenAnotherValidationIsActiveReturnsIt()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);
            IssueValidationRequestResult failed = await service.RequestAsync(findingId, TestContext.CancellationToken);
            await FailAsync(fixture, failed.Validation.Id);
            IssueValidationRequestResult active = await service.RequestAsync(findingId, TestContext.CancellationToken);

            IssueValidationRequestResult retry = await service.RetryAsync(failed.Validation.Id, TestContext.CancellationToken);

            Assert.IsTrue(retry.AlreadyActive);
            Assert.AreEqual(active.Validation.Id, retry.Validation.Id);
        }

        [TestMethod]
        public async Task Queries_ReturnLatestAndActiveValidation()
        {
            using ValidationTestFixture fixture = await CreateReadyFixtureAsync();
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);
            IssueValidationService service = CreateService(fixture);

            Assert.IsNull(await service.GetLatestAsync(findingId, TestContext.CancellationToken));
            Assert.IsNull(await service.GetActiveAsync(findingId, TestContext.CancellationToken));

            IssueValidationRequestResult first = await service.RequestAsync(findingId, TestContext.CancellationToken);

            Assert.AreEqual(first.Validation.Id, (await service.GetLatestAsync(findingId, TestContext.CancellationToken)).Id);
            Assert.AreEqual(first.Validation.Id, (await service.GetActiveAsync(findingId, TestContext.CancellationToken)).Id);
            Assert.IsEmpty(await service.GetLatestCompletedAsync([findingId], TestContext.CancellationToken));

            await CompleteAsync(fixture, first.Validation.Id);

            Assert.IsNull(await service.GetActiveAsync(findingId, TestContext.CancellationToken));
            Assert.HasCount(1, await service.GetLatestCompletedAsync([findingId], TestContext.CancellationToken));
        }

        private async Task<ValidationTestFixture> CreateReadyFixtureAsync()
        {
            ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync("class Example { }", "sha", TestContext.CancellationToken);
            await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken);
            return fixture;
        }

        private static IssueValidationService CreateService(ValidationTestFixture fixture)
        {
            return new IssueValidationService(
                fixture.Findings,
                fixture.Repositories,
                fixture.Validations,
                CreateConfiguration(fixture),
                new RecordingDiagnostics());
        }

        private static ReviewEngineConfigurationService CreateConfiguration(ValidationTestFixture fixture)
        {
            InMemorySecretsProvider secrets = new();
            secrets.Secrets[SecretName] = "secret";
            return new ReviewEngineConfigurationService(fixture.Settings, new ScriptedCompletionClient(), secrets);
        }

        private async Task CompleteAsync(ValidationTestFixture fixture, long validationId)
        {
            IssueValidation validation = await fixture.Validations.GetByIdAsync(validationId, TestContext.CancellationToken);
            validation.Status = IssueValidationStatus.Completed;
            validation.Verdict = IssueValidationVerdict.Agreed;
            validation.CompletedUtc = DateTime.UtcNow;
            validation.Summary = "valid";
            await fixture.Validations.CompleteAsync(validation, TestContext.CancellationToken);
        }

        private async Task FailAsync(ValidationTestFixture fixture, long validationId)
        {
            await fixture.Validations.FailAsync(
                validationId,
                "provider down",
                FailureCategory.Transient,
                string.Empty,
                new TokenUsage(),
                DateTime.UtcNow,
                TestContext.CancellationToken);
        }

        private sealed class NoRepositories : Revathryn.Contracts.Persistence.IRepositoryRepository
        {
            public Task<Repository> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Repository>(null);
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([]);
            }

            public Task<IReadOnlyList<Repository>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([]);
            }

            public Task<long> AddAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
