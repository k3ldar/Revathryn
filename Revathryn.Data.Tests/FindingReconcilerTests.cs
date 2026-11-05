using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class FindingReconcilerTests
    {
        private const string ReviewSha256 = "sha-256";
        private const string RuleKey = "rule:CA1000";
        private const string LocationKey = "loc:3:10:20";

        [TestMethod]
        public void Reconcile_NoPriorFindings_InsertsAllNewFindings()
        {
            FindingReconciler reconciler = new();
            List<Finding> newFindings = [CreateNewFinding(RuleKey, "fp-new")];

            FindingReconciliation result = reconciler.Reconcile([], newFindings, ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(1, result.FindingsToInsert);
            Assert.HasCount(0, result.FindingsToUpdate);
            Assert.HasCount(0, result.FindingsToResolve);
        }

        [TestMethod]
        public void Reconcile_KeyMatch_RefreshesContentAndPreservesTriage()
        {
            FindingReconciler reconciler = new();
            Finding existing = CreateExistingFinding(RuleKey, FindingStatus.Reviewed);
            existing.DuplicateOfFindingId = 42;
            existing.CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Finding candidate = CreateNewFinding(RuleKey, "fp-changed");

            FindingReconciliation result = reconciler.Reconcile([existing], [candidate], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(0, result.FindingsToInsert);
            Assert.HasCount(1, result.FindingsToUpdate);
            Assert.HasCount(0, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.Reviewed, existing.Status);
            Assert.AreEqual(42, existing.DuplicateOfFindingId);
            Assert.AreEqual(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), existing.CreatedDate);
            Assert.AreEqual("fp-changed", existing.Fingerprint);
        }

        [TestMethod]
        public void Reconcile_OpenCodeIssueAbsentFromNewFindings_IsAutoResolved()
        {
            FindingReconciler reconciler = new();
            Finding existing = CreateExistingFinding(RuleKey, FindingStatus.New);
            DateTime resolvedUtc = DateTime.UtcNow;

            FindingReconciliation result = reconciler.Reconcile([existing], [], ReviewSha256, resolvedUtc);

            Assert.HasCount(1, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.Resolved, existing.Status);
            Assert.AreEqual(FindingResolutionOrigin.Auto, existing.ResolutionOrigin);
            Assert.AreEqual(resolvedUtc, existing.ResolvedUtc);
            Assert.AreEqual(ReviewSha256, existing.ResolvedReviewSha256);
        }

        [TestMethod]
        public void Reconcile_OperationalFailureAbsentFromNewFindings_IsNotResolved()
        {
            FindingReconciler reconciler = new();
            Finding existing = CreateExistingFinding(LocationKey, FindingStatus.New, FindingCategory.OperationalFailure);

            FindingReconciliation result = reconciler.Reconcile([existing], [], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(0, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.New, existing.Status);
        }

        [TestMethod]
        public void Reconcile_FalsePositiveOrDeferredAbsentFromNewFindings_IsNotResolved()
        {
            FindingReconciler reconciler = new();
            Finding falsePositive = CreateExistingFinding(RuleKey, FindingStatus.FalsePositive);
            Finding deferred = CreateExistingFinding("rule:CA2000", FindingStatus.Deferred);

            FindingReconciliation result = reconciler.Reconcile([falsePositive, deferred], [], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(0, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.FalsePositive, falsePositive.Status);
            Assert.AreEqual(FindingStatus.Deferred, deferred.Status);
        }

        [TestMethod]
        public void Reconcile_ExistingWithoutKey_IsNotResolved()
        {
            FindingReconciler reconciler = new();
            Finding existing = CreateExistingFinding(string.Empty, FindingStatus.New);

            FindingReconciliation result = reconciler.Reconcile([existing], [], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(0, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.New, existing.Status);
        }

        [TestMethod]
        public void Reconcile_AutoResolvedFindingReappears_IsReopened()
        {
            FindingReconciler reconciler = new();
            Finding existing = CreateExistingFinding(RuleKey, FindingStatus.Resolved);
            existing.ResolutionOrigin = FindingResolutionOrigin.Auto;
            existing.ResolvedUtc = DateTime.UtcNow.AddDays(-1);
            existing.ResolvedReviewSha256 = "previous-sha";
            Finding candidate = CreateNewFinding(RuleKey, "fp-returned");

            FindingReconciliation result = reconciler.Reconcile([existing], [candidate], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(1, result.FindingsToUpdate);
            Assert.HasCount(0, result.FindingsToResolve);
            Assert.AreEqual(FindingStatus.New, existing.Status);
            Assert.AreEqual(FindingResolutionOrigin.None, existing.ResolutionOrigin);
            Assert.IsNull(existing.ResolvedUtc);
            Assert.AreEqual(string.Empty, existing.ResolvedReviewSha256);
        }

        [TestMethod]
        public void Reconcile_NewFindingWithoutKey_IsInserted()
        {
            FindingReconciler reconciler = new();
            Finding candidate = CreateNewFinding(string.Empty, "fp-keyless");

            FindingReconciliation result = reconciler.Reconcile([], [candidate], ReviewSha256, DateTime.UtcNow);

            Assert.HasCount(1, result.FindingsToInsert);
            Assert.HasCount(0, result.FindingsToUpdate);
            Assert.HasCount(0, result.FindingsToResolve);
        }

        [TestMethod]
        public void Reconcile_NullInputs_Throws()
        {
            FindingReconciler reconciler = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => reconciler.Reconcile(null, [], ReviewSha256, DateTime.UtcNow));
            Assert.ThrowsExactly<ArgumentNullException>(() => reconciler.Reconcile([], null, ReviewSha256, DateTime.UtcNow));
        }

        private static Finding CreateExistingFinding(string findingKey, FindingStatus status, FindingCategory category = FindingCategory.CodeIssue)
        {
            return new Finding
            {
                Id = 10,
                RepositoryId = 1,
                RepoFileId = 2,
                ReviewTypeId = 3,
                Fingerprint = "fp-existing",
                Severity = FindingSeverity.High,
                Title = "Existing title",
                Description = "Existing description",
                SuggestedFix = "Existing fix",
                Status = status,
                Category = category,
                FindingKey = findingKey,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        private static Finding CreateNewFinding(string findingKey, string fingerprint)
        {
            return new Finding
            {
                RepositoryId = 1,
                RepoFileId = 2,
                ReviewTypeId = 3,
                Fingerprint = fingerprint,
                Severity = FindingSeverity.High,
                Title = "New title",
                Description = "New description",
                SuggestedFix = "New fix",
                Status = FindingStatus.New,
                Category = FindingCategory.CodeIssue,
                FindingKey = findingKey,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }
    }
}
