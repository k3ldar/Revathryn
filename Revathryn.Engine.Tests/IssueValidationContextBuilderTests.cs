using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueValidationContextBuilderTests
    {
        private const string OriginalContent = "line one\nline two\nline three";
        private const string OriginalSha = "sha-original";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task BuildAsync_ReusesStoredDetectionContextAndOriginalPrompts()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(CreateDetectionContext(), TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationContextMode.OriginalSnapshot, context.Reference.Mode);
            Assert.AreEqual(contextId, context.Reference.ReviewContextId);
            Assert.IsFalse(context.Reference.SourceChangedSinceDetection);
            Assert.AreEqual(IssueValidationPrompts.SystemPrompt, context.SystemPrompt);
            Assert.AreEqual(IssueValidationPrompts.Version, context.PromptVersion);

            Assert.Contains("Potential missing null check", context.UserPrompt);
            Assert.Contains("The value can be null when it is dereferenced.", context.UserPrompt);
            Assert.Contains("Add a null guard.", context.UserPrompt);
            Assert.Contains("Severity: High", context.UserPrompt);
            Assert.Contains("C# Review", context.UserPrompt);
            Assert.Contains("ORIGINAL-SYSTEM-PROMPT", context.UserPrompt);
            Assert.Contains("ORIGINAL-REVIEW-PROMPT", context.UserPrompt);
            Assert.Contains("detect-model", context.UserPrompt);
            Assert.Contains("1: line one", context.UserPrompt);
            Assert.Contains("3: line three", context.UserPrompt);
        }

        [TestMethod]
        public async Task BuildAsync_DistinguishesRequestedFromSuppliedContextAndReportsTruncation()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(CreateDetectionContext(), TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.HasCount(2, context.Reference.RequestedContextKeys);
            Assert.AreEqual(2, context.Reference.ContextItemCount);
            Assert.AreEqual(1, context.Reference.TruncatedContextItemCount);
            Assert.Contains("Symbols:Missing, Symbols:Helper", context.UserPrompt);
            Assert.Contains("2 additional context item(s)", context.UserPrompt);
            Assert.Contains("TRUNCATED from 5000 characters", context.UserPrompt);
            Assert.Contains("helper body", context.UserPrompt);
        }

        [TestMethod]
        public async Task BuildAsync_RecordsSuppliedSourceLineCountForEvidenceChecking()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(CreateDetectionContext(), TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.HasCount(1, context.SuppliedSources);
            Assert.AreEqual(3, context.SuppliedSources[ValidationTestFixture.SourcePath]);
        }

        [TestMethod]
        public async Task BuildAsync_UsesOriginalSnapshotAndFlagsChangeWhenRepositoryHasChanged()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync("CURRENT-CHANGED-CONTENT", "sha-now-different", TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(CreateDetectionContext(), TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationContextMode.OriginalSnapshot, context.Reference.Mode);
            Assert.IsTrue(context.Reference.SourceChangedSinceDetection);
            Assert.AreEqual(OriginalSha, context.Reference.SuppliedSha256);
            Assert.AreEqual(OriginalSha, context.Reference.OriginalSha256);
            Assert.Contains("line one", context.UserPrompt);
            Assert.DoesNotContain("CURRENT-CHANGED-CONTENT", context.UserPrompt);
            Assert.IsNotEmpty(context.Reference.Notes);
        }

        [TestMethod]
        public async Task BuildAsync_FallsBackToCurrentSourceForLegacyFindingsAndSaysSo()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync("current line one\ncurrent line two", "sha-current", TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationContextMode.CurrentSource, context.Reference.Mode);
            Assert.IsNull(context.Reference.ReviewContextId);
            Assert.Contains("CURRENT repository source", context.UserPrompt);
            Assert.Contains("1: current line one", context.UserPrompt);
            Assert.Contains("original detection context was not stored", context.UserPrompt);
            Assert.IsNotEmpty(context.Reference.Notes);
            Assert.AreEqual(2, context.SuppliedSources[ValidationTestFixture.SourcePath]);
        }

        [TestMethod]
        public async Task BuildAsync_ReportsUnavailableWhenNoContextAndNoReadableSource()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(null, "sha", TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(null, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.AreEqual(IssueValidationContextMode.Unavailable, context.Reference.Mode);
            Assert.IsEmpty(context.SuppliedSources);
            Assert.Contains("No source code is available.", context.UserPrompt);
        }

        [TestMethod]
        public async Task BuildAsync_StatesWhenDetectionMetadataWasNotRecorded()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(
                new AIReviewContext { Sha256 = OriginalSha, FilePath = ValidationTestFixture.SourcePath, FileContent = OriginalContent },
                TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);

            IssueValidationContext context = await CreateBuilder(fixture).BuildAsync(await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken), TestContext.CancellationToken);

            Assert.Contains("Not recorded", context.UserPrompt);
            Assert.AreEqual(IssueValidationContextMode.OriginalSnapshot, context.Reference.Mode);
            Assert.IsNotEmpty(context.Reference.Notes);
        }

        [TestMethod]
        public async Task BuildAsync_DoesNotModifyTheFinding()
        {
            using ValidationTestFixture fixture = new();
            await fixture.InitialiseAsync(OriginalContent, OriginalSha, TestContext.CancellationToken);
            long contextId = await fixture.AddContextAsync(CreateDetectionContext(), TestContext.CancellationToken);
            long findingId = await fixture.AddFindingAsync(contextId, TestContext.CancellationToken);
            Finding before = await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);

            _ = await CreateBuilder(fixture).BuildAsync(before, TestContext.CancellationToken);

            Finding after = await fixture.Findings.GetByIdAsync(findingId, TestContext.CancellationToken);
            Assert.AreEqual(before.Title, after.Title);
            Assert.AreEqual(before.Description, after.Description);
            Assert.AreEqual(before.LastModifiedDate, after.LastModifiedDate);
        }

        [TestMethod]
        public async Task BuildAsync_ThrowsForNullFinding()
        {
            using ValidationTestFixture fixture = new();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => CreateBuilder(fixture).BuildAsync(null, TestContext.CancellationToken));
        }

        private static IssueValidationContextBuilder CreateBuilder(ValidationTestFixture fixture)
        {
            return new IssueValidationContextBuilder(fixture.Repositories, fixture.Files, fixture.ReviewTypes, fixture.Contexts);
        }

        private static AIReviewContext CreateDetectionContext()
        {
            return new AIReviewContext
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
                RequestedContextKeys = ["Symbols:Missing", "Symbols:Helper"],
                ContextItems =
                [
                    new ReviewContextItem { Title = "Helper", SourcePath = "src/Helper.cs", Content = "helper body", Truncated = false },
                    new ReviewContextItem { Title = "Big", SourcePath = "src/Big.cs", Content = "partial", Truncated = true, OriginalLength = 5000 }
                ]
            };
        }
    }
}
