using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DuplicateDetectionProcessorTests
    {
        private const long RepositoryId = 7;
        private const double AboveThresholdScore = 0.85;
        private const double BelowThresholdScore = 0.5;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task ProcessNextAsync_ReturnsFalseWhenNothingIsQueued()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            DuplicateDetectionProcessor processor = CreateProcessor(queue, findings, new FakeIssueSimilarityService());

            bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

            Assert.IsFalse(processed);
            Assert.HasCount(0, findings.MarkedDuplicates);
        }

        [TestMethod]
        public async Task ProcessNextAsync_ReturnsFalseWhenDetectionIsDisabled()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long findingId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "alpha beta gamma", Category = FindingCategory.CodeIssue });
            long queueId = await queue.EnqueueAsync(RepositoryId, findingId, TestContext.CancellationToken);
            IssueSimilarityOptions options = new() { Enabled = false };

            bool processed = await CreateProcessor(queue, findings, new FakeIssueSimilarityService(), options).ProcessNextAsync(TestContext.CancellationToken);

            Assert.IsFalse(processed);
            Assert.AreEqual(DuplicateDetectionStatus.Pending, queue.Items.Single(i => i.Id == queueId).Status);
        }

        [TestMethod]
        public async Task ProcessNextAsync_MarksFindingDuplicateWhenScoreMeetsThreshold()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long originalId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "original", Category = FindingCategory.CodeIssue });
            long duplicateId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "duplicate", Category = FindingCategory.CodeIssue });
            _ = await queue.EnqueueAsync(RepositoryId, duplicateId, TestContext.CancellationToken);
            FakeIssueSimilarityService similarity = new() { Scorer = (left, right) => AboveThresholdScore };
            IssueSimilarityOptions options = new() { Threshold = Consts.DefaultIssueSimilarityThreshold };

            bool processed = await CreateProcessor(queue, findings, similarity, options).ProcessNextAsync(TestContext.CancellationToken);

            Finding stored = await findings.GetByIdAsync(duplicateId, TestContext.CancellationToken);
            Assert.IsTrue(processed);
            Assert.AreEqual(originalId, stored.DuplicateOfFindingId.Value);
            Assert.AreEqual(AboveThresholdScore, stored.DuplicateSimilarityScore.Value);
            Assert.HasCount(1, findings.MarkedDuplicates);
        }

        [TestMethod]
        public async Task ProcessNextAsync_ResolvesOriginalThroughExistingDuplicateChain()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long originalId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "original", Category = FindingCategory.CodeIssue });
            long intermediateId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "intermediate", Category = FindingCategory.CodeIssue });
            long newId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "new", Category = FindingCategory.CodeIssue });
            await findings.MarkDuplicateAsync(intermediateId, originalId, 0.95, DateTime.UtcNow, TestContext.CancellationToken);
            _ = await queue.EnqueueAsync(RepositoryId, newId, TestContext.CancellationToken);
            FakeIssueSimilarityService similarity = new() { Scorer = (left, right) => right.Id == intermediateId ? 0.9 : BelowThresholdScore };
            IssueSimilarityOptions options = new() { Threshold = Consts.DefaultIssueSimilarityThreshold };

            _ = await CreateProcessor(queue, findings, similarity, options).ProcessNextAsync(TestContext.CancellationToken);

            Finding stored = await findings.GetByIdAsync(newId, TestContext.CancellationToken);
            Assert.AreEqual(originalId, stored.DuplicateOfFindingId.Value);
            Assert.AreEqual(0.9, stored.DuplicateSimilarityScore.Value);
        }

        [TestMethod]
        public async Task ProcessNextAsync_CompletesWithoutMarkingWhenScoreIsBelowThreshold()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long originalId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "original", Category = FindingCategory.CodeIssue });
            long duplicateId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "duplicate", Category = FindingCategory.CodeIssue });
            long queueId = await queue.EnqueueAsync(RepositoryId, duplicateId, TestContext.CancellationToken);
            FakeIssueSimilarityService similarity = new() { Scorer = (left, right) => BelowThresholdScore };
            IssueSimilarityOptions options = new() { Threshold = Consts.DefaultIssueSimilarityThreshold };

            _ = await CreateProcessor(queue, findings, similarity, options).ProcessNextAsync(TestContext.CancellationToken);

            Finding stored = await findings.GetByIdAsync(duplicateId, TestContext.CancellationToken);
            Assert.IsNull(stored.DuplicateOfFindingId);
            Assert.IsNull(stored.DuplicateSimilarityScore);
            Assert.AreEqual(DuplicateDetectionStatus.Completed, queue.Items.Single(i => i.Id == queueId).Status);
            Assert.HasCount(0, findings.MarkedDuplicates);
        }

        [TestMethod]
        public async Task ProcessNextAsync_CompletesWithoutMarkingWhenNoCandidatesExist()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long findingId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "lone finding", Category = FindingCategory.CodeIssue });
            long queueId = await queue.EnqueueAsync(RepositoryId, findingId, TestContext.CancellationToken);

            _ = await CreateProcessor(queue, findings, new FakeIssueSimilarityService()).ProcessNextAsync(TestContext.CancellationToken);

            Finding stored = await findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            Assert.IsNull(stored.DuplicateOfFindingId);
            Assert.AreEqual(DuplicateDetectionStatus.Completed, queue.Items.Single(i => i.Id == queueId).Status);
            Assert.HasCount(0, findings.MarkedDuplicates);
        }

        [TestMethod]
        public async Task ProcessNextAsync_FailsWhenFindingIsMissing()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long queueId = await queue.EnqueueAsync(RepositoryId, 9999, TestContext.CancellationToken);

            _ = await CreateProcessor(queue, findings, new FakeIssueSimilarityService()).ProcessNextAsync(TestContext.CancellationToken);

            DuplicateDetectionQueueItem item = queue.Items.Single(i => i.Id == queueId);
            Assert.AreEqual(DuplicateDetectionStatus.Failed, item.Status);
            Assert.AreEqual(Strings.DuplicateDetectionFindingNotFound, item.LastError);
        }

        [TestMethod]
        public async Task ProcessNextAsync_UnexpectedExceptionIsRetriedThenFailsWhenPolicyStops()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long originalId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "original", Category = FindingCategory.CodeIssue });
            long duplicateId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "duplicate", Category = FindingCategory.CodeIssue });
            long queueId = await queue.EnqueueAsync(RepositoryId, duplicateId, TestContext.CancellationToken);
            FakeIssueSimilarityService similarity = new() { Exception = new InvalidOperationException("boom") };
            TestRetryPolicy policy = new() { RetryAttempts = 1 };
            RecordingDiagnostics diagnostics = new();
            DuplicateDetectionProcessor processor = CreateProcessor(queue, findings, similarity, policy: policy, diagnostics: diagnostics);

            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            DuplicateDetectionQueueItem afterFirst = Copy(queue.Items.Single(i => i.Id == queueId));
            _ = await processor.ProcessNextAsync(TestContext.CancellationToken);
            DuplicateDetectionQueueItem afterSecond = Copy(queue.Items.Single(i => i.Id == queueId));

            Assert.AreEqual(DuplicateDetectionStatus.Pending, afterFirst.Status);
            Assert.AreEqual("boom", afterFirst.LastError);
            Assert.AreEqual(DuplicateDetectionStatus.Failed, afterSecond.Status);
            Assert.AreEqual("boom", afterSecond.LastError);
            Assert.IsTrue(diagnostics.Messages.Exists(m => m == "boom"));
        }

        [TestMethod]
        public async Task ProcessNextAsync_CancellationReleasesTheClaimAndRethrows()
        {
            FakeDuplicateDetectionQueue queue = new();
            FakeFindingRepository findings = new();
            long originalId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "original", Category = FindingCategory.CodeIssue });
            long duplicateId = findings.Add(new Finding { RepositoryId = RepositoryId, Title = "duplicate", Category = FindingCategory.CodeIssue });
            long queueId = await queue.EnqueueAsync(RepositoryId, duplicateId, TestContext.CancellationToken);
            using CancellationTokenSource source = new();
            source.Cancel();
            FakeIssueSimilarityService similarity = new() { Exception = new OperationCanceledException(source.Token) };
            DuplicateDetectionProcessor processor = CreateProcessor(queue, findings, similarity);

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ProcessNextAsync(source.Token));

            DuplicateDetectionQueueItem item = queue.Items.Single(i => i.Id == queueId);
            Assert.AreEqual(DuplicateDetectionStatus.Pending, item.Status);
            Assert.IsNull(item.LeasedUntilUtc);
        }

        private static DuplicateDetectionProcessor CreateProcessor(
            FakeDuplicateDetectionQueue queue,
            FakeFindingRepository findings,
            IIssueSimilarityService similarity,
            IssueSimilarityOptions options = null,
            TestRetryPolicy policy = null,
            RecordingDiagnostics diagnostics = null)
        {
            IssueSimilarityOptions resolved = options ?? new IssueSimilarityOptions();
            IssueSettingsProvider provider = new();
            provider.Refresh(new AISettings
            {
                IssueSimilarityEnabled = resolved.Enabled,
                IssueSimilarityThreshold = resolved.Threshold,
                IssueSimilarityMinimumTokenLength = resolved.MinimumTokenLength
            });

            return new DuplicateDetectionProcessor(
                queue,
                findings,
                similarity,
                provider,
                new TestRetryPolicyProvider(policy ?? new TestRetryPolicy()),
                new ResilienceOptions(),
                diagnostics ?? new RecordingDiagnostics());
        }

        private static DuplicateDetectionQueueItem Copy(DuplicateDetectionQueueItem item)
        {
            return new DuplicateDetectionQueueItem
            {
                Id = item.Id,
                RepositoryId = item.RepositoryId,
                FindingId = item.FindingId,
                Status = item.Status,
                AttemptCount = item.AttemptCount,
                CreatedUtc = item.CreatedUtc,
                NextAttemptUtc = item.NextAttemptUtc,
                LeasedUntilUtc = item.LeasedUntilUtc,
                LastError = item.LastError
            };
        }

        private sealed class FakeIssueSimilarityService : IIssueSimilarityService
        {
            public Func<Finding, Finding, double> Scorer { get; set; } = (left, right) => Consts.IssueSimilarityMinimumScore;

            public Exception Exception { get; set; }

            public double CalculateSimilarity(Finding left, Finding right)
            {
                if (Exception is not null)
                {
                    throw Exception;
                }

                return Scorer(left, right);
            }

            public bool IsDuplicate(Finding left, Finding right, double threshold)
            {
                return CalculateSimilarity(left, right) >= threshold;
            }

            public IReadOnlySet<string> Tokenize(string text)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeDuplicateDetectionQueue : IDuplicateDetectionQueue
        {
            private readonly List<DuplicateDetectionQueueItem> _items = [];
            private long _nextId = 1;

            public IReadOnlyList<DuplicateDetectionQueueItem> Items
            {
                get { return _items; }
            }

            public Task<long> EnqueueAsync(long repositoryId, long findingId, CancellationToken cancellationToken = default)
            {
                DuplicateDetectionQueueItem item = new()
                {
                    Id = _nextId++,
                    RepositoryId = repositoryId,
                    FindingId = findingId,
                    Status = DuplicateDetectionStatus.Pending,
                    CreatedUtc = DateTime.UtcNow
                };

                _items.Add(item);
                return Task.FromResult(item.Id);
            }

            public Task<DuplicateDetectionQueueItem> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            {
                DuplicateDetectionQueueItem item = _items
                    .Where(i => i.Status == DuplicateDetectionStatus.Pending && (i.NextAttemptUtc is null || i.NextAttemptUtc <= utcNow))
                    .OrderBy(i => i.CreatedUtc)
                    .ThenBy(i => i.Id)
                    .FirstOrDefault();

                if (item is null)
                {
                    return Task.FromResult<DuplicateDetectionQueueItem>(null);
                }

                item.Status = DuplicateDetectionStatus.Running;
                item.AttemptCount++;
                item.LeasedUntilUtc = utcNow.Add(leaseDuration);
                return Task.FromResult(item);
            }

            public Task CompleteAsync(long id, CancellationToken cancellationToken = default)
            {
                SetStatus(id, DuplicateDetectionStatus.Completed);
                return Task.CompletedTask;
            }

            public Task FailAsync(long id, string errorMessage, CancellationToken cancellationToken = default)
            {
                DuplicateDetectionQueueItem item = _items.FirstOrDefault(i => i.Id == id);

                if (item is not null)
                {
                    item.Status = DuplicateDetectionStatus.Failed;
                    item.NextAttemptUtc = null;
                    item.LeasedUntilUtc = null;
                    item.LastError = errorMessage ?? string.Empty;
                }

                return Task.CompletedTask;
            }

            public Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string errorMessage, CancellationToken cancellationToken = default)
            {
                DuplicateDetectionQueueItem item = _items.FirstOrDefault(i => i.Id == id);

                if (item is not null)
                {
                    item.Status = DuplicateDetectionStatus.Pending;
                    item.NextAttemptUtc = nextAttemptUtc;
                    item.LeasedUntilUtc = null;
                    item.LastError = errorMessage ?? string.Empty;
                }

                return Task.CompletedTask;
            }

            private void SetStatus(long id, DuplicateDetectionStatus status)
            {
                DuplicateDetectionQueueItem item = _items.FirstOrDefault(i => i.Id == id);

                if (item is not null)
                {
                    item.Status = status;
                    item.NextAttemptUtc = null;
                    item.LeasedUntilUtc = null;
                }
            }
        }

        private sealed class FakeFindingRepository : IFindingRepository
        {
            private readonly Dictionary<long, Finding> _findings = [];
            private long _nextId = 1;

            public List<(long FindingId, long DuplicateOfFindingId, double Score, DateTime LastModifiedUtc)> MarkedDuplicates { get; } = [];

            public long Add(Finding finding)
            {
                finding.Id = _nextId++;
                _findings[finding.Id] = finding;
                return finding.Id;
            }

            public Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                _findings.TryGetValue(id, out Finding finding);
                return Task.FromResult(finding);
            }

            public Task<IReadOnlyList<Finding>> GetSimilarityCandidatesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                List<Finding> candidates = [.. _findings.Values
                    .Where(f => f.RepositoryId == repositoryId)
                    .OrderBy(f => f.Id)];

                return Task.FromResult<IReadOnlyList<Finding>>(candidates);
            }

            public Task MarkDuplicateAsync(long findingId, long duplicateOfFindingId, double similarityScore, DateTime lastModifiedUtc, CancellationToken cancellationToken = default)
            {
                MarkedDuplicates.Add((findingId, duplicateOfFindingId, similarityScore, lastModifiedUtc));

                if (_findings.TryGetValue(findingId, out Finding finding))
                {
                    finding.DuplicateOfFindingId = duplicateOfFindingId;
                    finding.DuplicateSimilarityScore = similarityScore;
                    finding.LastModifiedDate = lastModifiedUtc;
                }

                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<Finding> GetByFingerprintAsync(long repositoryId, string fingerprint, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<FindingNavigation> GetNavigationAsync(long findingId, FindingQuery query, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<RepositoryIssueCount>> CountByRepositoryAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
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
