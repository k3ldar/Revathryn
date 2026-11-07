using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RepositoryProcessingTests
    {
        [TestMethod]
        public async Task RepositoryScanner_IgnoresUnsupportedExtensions()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                File.WriteAllText(Path.Combine(root, "notes.txt"), "not scanned");

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("keep.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ScansNestedDirectories()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
                File.WriteAllText(Path.Combine(root, "src", "nested", "Program.cs"), "class Program { }");

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("src/nested/Program.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_AppliesCustomExclusions()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "vendor"));
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                File.WriteAllText(Path.Combine(root, "vendor", "ignored.cs"), "class Ignored { }");

                RepositoryScanOptions options = new()
                {
                    ExcludedDirectoryNames = ["vendor"]
                };

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, options, cancellationToken: TestContext.CancellationToken);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("keep.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ReturnsStableHashForUnchangedContent()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                RepositoryScanner scanner = new();
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);
                RepositoryScanResult second = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.AreEqual(first.DiscoveredFiles[0].Sha256, second.DiscoveredFiles[0].Sha256);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ReturnsDifferentHashForChangedContent()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                RepositoryScanner scanner = new();
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                File.WriteAllText(filePath, "class Program { void M() { } }");
                RepositoryScanResult second = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.AreNotEqual(first.DiscoveredFiles[0].Sha256, second.DiscoveredFiles[0].Sha256);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_DetectsContentChangeWhenTimestampUnchanged()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                DateTime originalTimestamp = File.GetLastWriteTimeUtc(filePath);

                RepositoryScanner scanner = new();
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                File.WriteAllText(filePath, "class Program { void M() { } }");
                File.SetLastWriteTimeUtc(filePath, originalTimestamp);

                RepositoryScanResult second = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.AreEqual(originalTimestamp, File.GetLastWriteTimeUtc(filePath));
                Assert.AreNotEqual(first.DiscoveredFiles[0].Sha256, second.DiscoveredFiles[0].Sha256);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ReportsLockedFileAsFailure()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Locked.cs");
                File.WriteAllText(filePath, "class Locked { }");

                using FileStream locked = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                Assert.HasCount(0, scan.DiscoveredFiles);
                Assert.HasCount(1, scan.Failures);
                Assert.AreEqual("Locked.cs", scan.Failures[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ReusesFingerprintForUnchangedFile()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                CountingFileHasher hasher = new();
                RepositoryScanner scanner = new(hasher);
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                IReadOnlyDictionary<string, RepoFile> existing = BuildExistingFiles(first.DiscoveredFiles);
                RepositoryScanResult second = await scanner.ScanAsync(root, existingFiles: existing, cancellationToken: TestContext.CancellationToken);

                Assert.AreEqual(1, hasher.AsyncCallCount);
                Assert.AreEqual(first.DiscoveredFiles[0].Sha256, second.DiscoveredFiles[0].Sha256);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ForceFullScanRehashesUnchangedFile()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                CountingFileHasher hasher = new();
                RepositoryScanner scanner = new(hasher);
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                IReadOnlyDictionary<string, RepoFile> existing = BuildExistingFiles(first.DiscoveredFiles);
                await scanner.ScanAsync(root, existingFiles: existing, forceFullScan: true, cancellationToken: TestContext.CancellationToken);

                Assert.AreEqual(2, hasher.AsyncCallCount);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_RehashesWhenTimestampChanges()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                CountingFileHasher hasher = new();
                RepositoryScanner scanner = new(hasher);
                RepositoryScanResult first = await scanner.ScanAsync(root, cancellationToken: TestContext.CancellationToken);

                File.SetLastWriteTimeUtc(filePath, File.GetLastWriteTimeUtc(filePath).AddSeconds(1));

                IReadOnlyDictionary<string, RepoFile> existing = BuildExistingFiles(first.DiscoveredFiles);
                RepositoryScanResult second = await scanner.ScanAsync(root, existingFiles: existing, cancellationToken: TestContext.CancellationToken);

                Assert.AreEqual(2, hasher.AsyncCallCount);
                Assert.AreEqual(first.DiscoveredFiles[0].Sha256, second.DiscoveredFiles[0].Sha256);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_RejectsBlankPath()
        {
            FakeRepositoryRepository repositories = new();
            FakeReviewTypeRepository reviewTypes = new();
            RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = "  " }, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RepositoryService_RejectsNonexistentDirectory()
        {
            string missing = Path.Combine(CreateTempDirectory(), "does-not-exist");

            FakeRepositoryRepository repositories = new();
            FakeReviewTypeRepository reviewTypes = new();
            RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

            await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = missing }, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RepositoryService_RejectsDuplicateRepository()
        {
            string root = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root }, TestContext.CancellationToken);

                string duplicatePath = root + Path.DirectorySeparatorChar;

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RegisterAsync(new Repository { Name = "Repo Again", LocalPath = duplicatePath }, TestContext.CancellationToken));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_NormalisesStoredPath()
        {
            string root = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                Repository registered = await service.RegisterAsync(new Repository
                {
                    Name = "Repo",
                    LocalPath = root + Path.DirectorySeparatorChar
                }, TestContext.CancellationToken);

                Assert.AreEqual(Path.TrimEndingDirectorySeparator(root), registered.LocalPath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_SetEnabledTogglesRepository()
        {
            string root = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root }, TestContext.CancellationToken);

                await service.SetEnabledAsync(registered.Id, false, TestContext.CancellationToken);

                Repository disabled = await service.GetAsync(registered.Id, TestContext.CancellationToken);
                Assert.IsFalse(disabled.Enabled);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioA_NewFileQueuesReview()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, result.Added);
            Assert.HasCount(1, await harness.Queue.GetPendingAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioB_ChangedFileQueuesReview()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.SeedAnalysis(1, 1, "old-hash", "v1");

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, result.Added);
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioC_UnchangedFileNotQueued()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.SeedAnalysis(1, 1, "h1", "v1");

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioD_NewReviewTypeQueuesExistingFiles()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.SeedAnalysis(1, 1, "h1", "v1");
            harness.SeedReviewType(2, "cs", "v1", true, 20);

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, result.Added);
            IReadOnlyList<QueueItem> pending = await harness.Queue.GetPendingAsync(TestContext.CancellationToken);
            Assert.HasCount(1, pending);
            Assert.AreEqual(2, pending[0].ReviewTypeId);
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioE_EditedDefinitionRequeues()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            ReviewType reviewType = harness.SeedReviewType(1, "cs", "v1");
            harness.SeedAnalysis(1, 1, "h1", "v1");

            reviewType.ReviewDefinitionVersion = "v2";

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, result.Added);
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioF_DisabledReviewTypeIgnored()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1", enabled: false);

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioG_DisabledLinkIgnored()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.Links.Add(new ReviewLink { RepoFileId = 1, ReviewTypeId = 1, Enabled = false });

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(0, result.Added);
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioH_ReEnabledReviewTypeBecomesEligible()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.Links.Add(new ReviewLink { RepoFileId = 1, ReviewTypeId = 1, Enabled = true });

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, result.Added);
        }

        [TestMethod]
        public async Task ReviewScheduler_UsesPriorityOverrideFromLink()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1", true, 10);
            harness.Links.Add(new ReviewLink { RepoFileId = 1, ReviewTypeId = 1, Enabled = true, PriorityOverride = 500 });

            await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            QueueItem item = await harness.Queue.ClaimNextAsync(DateTime.UtcNow.AddMinutes(5), TestContext.CancellationToken);
            Assert.AreEqual(500, item.Priority);
        }

        [TestMethod]
        public async Task ReviewScheduler_RepeatedScheduleDoesNotDuplicate()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");

            ReviewScheduleResult first = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);
            ReviewScheduleResult second = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(1, first.Added);
            Assert.AreEqual(0, second.Added);
            Assert.AreEqual(1, second.DuplicatesSkipped);
            Assert.AreEqual(1, await harness.Queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RepositoryProcessor_ScanPopulatesSummaryAndQueue()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                RepositoryScanSummary summary = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, summary.FilesDiscovered);
                Assert.AreEqual(1, summary.NewFiles);
                Assert.AreEqual(1, summary.CSharpFilesAnalysed);
                Assert.AreEqual(1, summary.QueueEntriesAdded);
                Assert.AreEqual(1, await harness.Queue.CountAsync(TestContext.CancellationToken));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_RepeatedScanDoesNotDuplicateQueueEntries()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                await harness.Processor.ProcessAsync(1, CancellationToken.None);
                harness.AnalysisEngine.RequiresAnalysisResult = false;

                RepositoryScanSummary second = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, second.UnchangedFiles);
                Assert.AreEqual(0, second.QueueEntriesAdded);
                Assert.AreEqual(1, second.QueueDuplicatesSkipped);
                Assert.AreEqual(1, await harness.Queue.CountAsync(TestContext.CancellationToken));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_DetectsRemovedFiles()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                await harness.Processor.ProcessAsync(1, CancellationToken.None);

                File.Delete(filePath);
                harness.AnalysisEngine.RequiresAnalysisResult = false;

                RepositoryScanSummary second = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, second.RemovedFiles);
                IReadOnlyList<RepoFile> remaining = await harness.Files.GetByRepositoryAsync(1, TestContext.CancellationToken);
                Assert.HasCount(1, remaining);
                Assert.IsTrue(remaining[0].IsRemoved);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_CountsCSharpAnalysisFailure()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });
                harness.AnalysisEngine.ThrowOnAnalyse = true;

                RepositoryScanSummary summary = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(0, summary.CSharpFilesAnalysed);
                Assert.AreEqual(1, summary.CSharpAnalysisFailures);
                Assert.AreEqual(1, summary.QueueEntriesAdded);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_ProcessAllEnabled_SkipsDisabledRepositories()
        {
            string enabledRoot = CreateTempDirectory();
            string disabledRoot = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Enabled", LocalPath = enabledRoot, Enabled = true });
                repositories.Add(new Repository { Id = 2, Name = "Disabled", LocalPath = disabledRoot, Enabled = false });

                FakeRepoFileRepository files = new();
                FakeReviewTypeRepository reviewTypes = new();
                FakeReviewLinkRepository links = new();
                FakeRepoFileAnalysisRepository analyses = new();
                FakeQueueRepository queue = new();
                FakeCSharpAnalysisEngine analysisEngine = new();
                FakeProjectDiscoveryService projectDiscovery = new();
                FakeProjectRepository projects = new();
                FakeRepositoryScanRepository scans = new();
                ReviewScheduler scheduler = new(repositories, files, reviewTypes, links, analyses, new ReviewQueue(queue));
                RepositoryProcessor processor = new(repositories, files, new RepositoryScanner(), scheduler, analysisEngine, projectDiscovery, projects, scans, new RepositoryScanLockRegistry());

                IReadOnlyList<RepositoryScanSummary> summaries = await processor.ProcessAllEnabledAsync(CancellationToken.None);

                Assert.HasCount(1, summaries);
                Assert.AreEqual(1, summaries[0].RepositoryId);
            }
            finally
            {
                Directory.Delete(enabledRoot, true);
                Directory.Delete(disabledRoot, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_RejectsFileAsPath()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "not-a-directory.txt");
                File.WriteAllText(filePath, "content");

                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = filePath }, TestContext.CancellationToken));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_UpdateConfiguration()
        {
            string root = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root, Description = "Before" }, TestContext.CancellationToken);

                registered.Description = "After";
                registered.ScanOptions = new RepositoryScanOptions { RespectGitIgnore = true };
                await service.UpdateAsync(registered, TestContext.CancellationToken);

                Repository updated = await service.GetAsync(registered.Id, TestContext.CancellationToken);

                Assert.AreEqual("After", updated.Description);
                Assert.IsTrue(updated.ScanOptions.RespectGitIgnore);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_RemoveRegistration()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");

                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), new FakeRepositoryProcessor());

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root }, TestContext.CancellationToken);
                await service.DeleteAsync(registered.Id, TestContext.CancellationToken);

                Assert.IsNull(await service.GetAsync(registered.Id, TestContext.CancellationToken));
                Assert.IsTrue(Directory.Exists(root));
                Assert.IsTrue(File.Exists(Path.Combine(root, "keep.cs")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_RegisterAutomaticRepository_TriggersInitialReview()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");

                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                FakeRepositoryProcessor processor = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), processor);

                Repository registered = await service.RegisterAsync(new Repository
                {
                    Name = "Repo",
                    LocalPath = root,
                    Enabled = true,
                    AutomaticReviewEnabled = true
                }, TestContext.CancellationToken);

                Assert.HasCount(1, processor.ProcessedIds);
                Assert.AreEqual(registered.Id, processor.ProcessedIds[0]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryService_RegisterDisabledOrNonAutomaticRepository_DoesNotTriggerInitialReview()
        {
            string root = CreateTempDirectory();
            try
            {
                string manualRoot = Path.Combine(root, "manual");
                Directory.CreateDirectory(manualRoot);

                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                FakeRepositoryProcessor processor = new();
                RepositoryService service = new(repositories, new ReviewTypeSeeder(reviewTypes), processor);

                await service.RegisterAsync(new Repository { Name = "Disabled", LocalPath = root, Enabled = false, AutomaticReviewEnabled = true }, TestContext.CancellationToken);
                await service.RegisterAsync(new Repository { Name = "Manual", LocalPath = manualRoot, Enabled = true, AutomaticReviewEnabled = false }, TestContext.CancellationToken);

                Assert.HasCount(0, processor.ProcessedIds);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewScheduler_SkipsRemovedFiles()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1", isRemoved: true);
            harness.SeedReviewType(1, "cs", "v1");

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1, TestContext.CancellationToken);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task RepositoryProcessor_AddedFile_IncrementsNewFiles()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                await harness.Processor.ProcessAsync(1, CancellationToken.None);

                File.WriteAllText(Path.Combine(root, "Second.cs"), "class Second { }");
                harness.AnalysisEngine.RequiresAnalysisResult = false;

                RepositoryScanSummary second = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(2, second.FilesDiscovered);
                Assert.AreEqual(1, second.NewFiles);
                Assert.AreEqual(1, second.UnchangedFiles);
                Assert.AreEqual(0, second.ChangedFiles);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_ModifiedFile_IncrementsChangedFiles()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                await harness.Processor.ProcessAsync(1, CancellationToken.None);

                File.WriteAllText(filePath, "class Program { void M() { } }");
                harness.AnalysisEngine.RequiresAnalysisResult = false;

                RepositoryScanSummary second = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, second.ChangedFiles);
                Assert.AreEqual(0, second.UnchangedFiles);
                Assert.AreEqual(0, second.NewFiles);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_ReintroducedFile_TreatedAsUnchangedWhenContentMatches()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(filePath, "class Program { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                await harness.Processor.ProcessAsync(1, CancellationToken.None);

                File.Delete(filePath);
                harness.AnalysisEngine.RequiresAnalysisResult = false;
                RepositoryScanSummary removed = await harness.Processor.ProcessAsync(1, CancellationToken.None);
                Assert.AreEqual(1, removed.RemovedFiles);

                File.WriteAllText(filePath, "class Program { }");
                RepositoryScanSummary reintroduced = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, reintroduced.UnchangedFiles);
                Assert.AreEqual(0, reintroduced.NewFiles);
                Assert.AreEqual(0, reintroduced.RemovedFiles);

                RepoFile file = await harness.Files.GetByRelativePathAsync(1, "Program.cs", TestContext.CancellationToken);
                Assert.IsFalse(file.IsRemoved);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_PartialScanFailure_PreservesFailedFile()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");
                string lockedPath = Path.Combine(root, "Locked.cs");
                File.WriteAllText(lockedPath, "class Locked { }");

                ProcessorHarness harness = new(root);
                harness.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });

                using (FileStream locked = new(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    RepositoryScanSummary summary = await harness.Processor.ProcessAsync(1, CancellationToken.None);

                    Assert.AreEqual(1, summary.FilesDiscovered);
                    Assert.AreEqual(1, summary.NewFiles);
                    Assert.AreEqual(0, summary.RemovedFiles);
                }

                Assert.HasCount(1, harness.Scans.Records);
                Assert.IsTrue(harness.Scans.Records[0].Succeeded);
                Assert.AreEqual(1, harness.Scans.Records[0].FilesDiscovered);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_InterruptedScan_CanBeRepeated()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });
                FakeReviewLinkRepository links = new();
                FakeRepoFileAnalysisRepository analyses = new();
                FakeQueueRepository queue = new();
                FakeCSharpAnalysisEngine analysisEngine = new();
                FakeProjectDiscoveryService projectDiscovery = new();
                FakeProjectRepository projects = new();
                FakeRepositoryScanRepository scans = new();
                FlakyScanner scanner = new();

                ReviewScheduler scheduler = new(repositories, files, reviewTypes, links, analyses, new ReviewQueue(queue));
                RepositoryProcessor processor = new(repositories, files, scanner, scheduler, analysisEngine, projectDiscovery, projects, scans, new RepositoryScanLockRegistry());

                await Assert.ThrowsExactlyAsync<IOException>(() => processor.ProcessAsync(1, CancellationToken.None));

                Assert.HasCount(1, scans.Records);
                Assert.IsFalse(scans.Records[0].Succeeded);

                RepositoryScanSummary recovered = await processor.ProcessAsync(1, CancellationToken.None);

                Assert.AreEqual(1, recovered.FilesDiscovered);
                Assert.AreEqual(1, recovered.NewFiles);
                Assert.HasCount(2, scans.Records);
                Assert.IsTrue(scans.Records[^1].Succeeded);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryProcessor_FailedScanWithoutChanges_IsRecordedAsFailed()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true });
                FakeReviewLinkRepository links = new();
                FakeRepoFileAnalysisRepository analyses = new();
                FakeQueueRepository queue = new();
                FakeCSharpAnalysisEngine analysisEngine = new();
                FakeProjectDiscoveryService projectDiscovery = new();
                FakeProjectRepository projects = new();
                FakeRepositoryScanRepository scans = new();
                FlakyScanner scanner = new();

                ReviewScheduler scheduler = new(repositories, files, reviewTypes, links, analyses, new ReviewQueue(queue));
                RepositoryProcessor processor = new(repositories, files, scanner, scheduler, analysisEngine, projectDiscovery, projects, scans, new RepositoryScanLockRegistry());

                await Assert.ThrowsExactlyAsync<IOException>(() => processor.ProcessAsync(1, CancellationToken.None));

                Assert.HasCount(1, scans.Records);
                Assert.IsFalse(scans.Records[0].Succeeded);
                Assert.AreEqual(0, scans.Records[0].FilesDiscovered);
                Assert.IsFalse(scans.Records[0].HasChanges);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-phase3-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static IReadOnlyDictionary<string, RepoFile> BuildExistingFiles(IReadOnlyList<RepoFile> files)
        {
            Dictionary<string, RepoFile> map = [with(StringComparer.Ordinal)];

            foreach (RepoFile file in files)
            {
                map[file.RelativePath] = file;
            }

            return map;
        }

        private sealed class SchedulerHarness
        {
            public SchedulerHarness()
            {
                Scheduler = new ReviewScheduler(Repositories, Files, ReviewTypes, Links, Analyses, new ReviewQueue(Queue));
            }

            public FakeRepositoryRepository Repositories { get; } = new();

            public FakeRepoFileRepository Files { get; } = new();

            public FakeReviewTypeRepository ReviewTypes { get; } = new();

            public FakeReviewLinkRepository Links { get; } = new();

            public FakeRepoFileAnalysisRepository Analyses { get; } = new();

            public FakeQueueRepository Queue { get; } = new();

            public ReviewScheduler Scheduler { get; }

            public void SeedRepository()
            {
                Repositories.Add(new Repository { Id = 1, Name = "Repo", Enabled = true });
            }

            public void SeedFile(long id, string relativePath, string extension, string sha256, bool isRemoved = false)
            {
                Files.Add(new RepoFile
                {
                    Id = id,
                    RepositoryId = 1,
                    RelativePath = relativePath,
                    Extension = extension,
                    Sha256 = sha256,
                    IsRemoved = isRemoved
                });
            }

            public ReviewType SeedReviewType(long id, string fileExtensions, string version, bool enabled = true, int priority = 10)
            {
                ReviewType reviewType = new()
                {
                    Id = id,
                    RepositoryId = 1,
                    FileExtensions = fileExtensions,
                    ReviewDefinitionVersion = version,
                    DefaultPriority = priority,
                    Enabled = enabled
                };

                ReviewTypes.Add(reviewType);
                return reviewType;
            }

            public void SeedAnalysis(long repoFileId, long reviewTypeId, string sha256, string reviewDefinitionVersion)
            {
                Analyses.Add(new RepoFileAnalysis
                {
                    RepositoryId = 1,
                    RepoFileId = repoFileId,
                    ReviewTypeId = reviewTypeId,
                    Sha256 = sha256,
                    ReviewDefinitionVersion = reviewDefinitionVersion,
                    AnalysisVersion = Consts.AnalysisVersion
                });
            }
        }

        private sealed class ProcessorHarness
        {
            public ProcessorHarness(string localPath)
            {
                Repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = localPath, Enabled = true });

                ReviewScheduler scheduler = new(Repositories, Files, ReviewTypes, Links, Analyses, new ReviewQueue(Queue));
                Processor = new RepositoryProcessor(Repositories, Files, new RepositoryScanner(), scheduler, AnalysisEngine, ProjectDiscovery, Projects, Scans, new RepositoryScanLockRegistry());
            }

            public FakeRepositoryRepository Repositories { get; } = new();

            public FakeRepoFileRepository Files { get; } = new();

            public FakeReviewTypeRepository ReviewTypes { get; } = new();

            public FakeReviewLinkRepository Links { get; } = new();

            public FakeRepoFileAnalysisRepository Analyses { get; } = new();

            public FakeQueueRepository Queue { get; } = new();

            public FakeCSharpAnalysisEngine AnalysisEngine { get; } = new();

            public FakeProjectDiscoveryService ProjectDiscovery { get; } = new();

            public FakeProjectRepository Projects { get; } = new();

            public FakeRepositoryScanRepository Scans { get; } = new();

            public RepositoryProcessor Processor { get; }
        }

        private sealed class FakeRepositoryRepository : IRepositoryRepository
        {
            private readonly Dictionary<long, Repository> _items = [];
            private long _nextId = 1;

            public void Add(Repository repository)
            {
                if (repository.Id == 0)
                {
                    repository.Id = _nextId++;
                }

                _items[repository.Id] = repository;
            }

            public Task<Repository> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue(id, out Repository repository);
                return Task.FromResult(repository);
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([.. _items.Values]);
            }

            public Task<IReadOnlyList<Repository>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([.. _items.Values.Where(r => r.Enabled)]);
            }

            public Task<long> AddAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                Add(repository);
                return Task.FromResult(repository.Id);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                _items[repository.Id] = repository;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.Remove(id);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepoFileRepository : IRepoFileRepository
        {
            private readonly Dictionary<(long RepositoryId, string RelativePath), RepoFile> _items = [];
            private long _nextId = 1;

            public void Add(RepoFile file)
            {
                if (file.Id == 0)
                {
                    file.Id = _nextId++;
                }

                _items[(file.RepositoryId, file.RelativePath)] = file;
            }

            public Task<RepoFile> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                RepoFile file = _items.Values.FirstOrDefault(f => f.Id == id);
                return Task.FromResult(file);
            }

            public Task<IReadOnlyList<RepoFile>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepoFile>>([.. _items.Values.Where(f => f.RepositoryId == repositoryId)]);
            }

            public Task<RepoFile> GetByRelativePathAsync(long repositoryId, string relativePath, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue((repositoryId, relativePath), out RepoFile file);
                return Task.FromResult(file);
            }

            public Task<RepositoryReconciliationResult> ReconcileAsync(
                long repositoryId,
                IReadOnlyList<RepoFile> discoveredFiles,
                IReadOnlyCollection<string> failedPaths,
                CancellationToken cancellationToken = default)
            {
                RepositoryReconciliationResult result = new();
                List<RepositoryFileChange> changes = [];
                HashSet<string> discoveredPaths = [with(StringComparer.Ordinal)];
                HashSet<string> failedSet = failedPaths is null
                    ? [with(StringComparer.Ordinal)]
                    : new HashSet<string>(failedPaths, StringComparer.Ordinal);
                List<RepoFile> addedCandidates = [];
                List<RepoFile> removedCandidates = [];

                foreach (RepoFile file in discoveredFiles)
                {
                    file.RepositoryId = repositoryId;
                    file.IsRemoved = false;
                    discoveredPaths.Add(file.RelativePath);

                    (long RepositoryId, string RelativePath) key = (repositoryId, file.RelativePath);

                    if (_items.TryGetValue(key, out RepoFile existing))
                    {
                        file.Id = existing.Id;

                        if (string.Equals(existing.Sha256, file.Sha256, StringComparison.Ordinal))
                        {
                            result.UnchangedFiles++;
                            changes.Add(BuildChange(repositoryId, file, RepositoryChangeType.Unchanged));
                        }
                        else
                        {
                            result.ChangedFiles++;
                            changes.Add(BuildChange(repositoryId, file, RepositoryChangeType.Modified));
                        }
                    }
                    else
                    {
                        if (file.Id == 0)
                        {
                            file.Id = _nextId++;
                        }

                        addedCandidates.Add(file);
                    }

                    _items[key] = file;
                }

                foreach (RepoFile existing in _items.Values.Where(f => f.RepositoryId == repositoryId).ToList())
                {
                    if (discoveredPaths.Contains(existing.RelativePath) || failedSet.Contains(existing.RelativePath) || existing.IsRemoved)
                    {
                        continue;
                    }

                    removedCandidates.Add(existing);
                    existing.IsRemoved = true;
                }

                HashSet<string> matchedAddedPaths = ClassifyAdditionsAndRemovals(repositoryId, addedCandidates, removedCandidates, changes, result);

                foreach (RepoFile added in addedCandidates)
                {
                    if (!matchedAddedPaths.Contains(added.RelativePath))
                    {
                        result.NewFiles++;
                        changes.Add(BuildChange(repositoryId, added, RepositoryChangeType.Added));
                    }
                }

                result.Changes = changes;
                return Task.FromResult(result);
            }

            private static HashSet<string> ClassifyAdditionsAndRemovals(
                long repositoryId,
                List<RepoFile> addedCandidates,
                List<RepoFile> removedCandidates,
                List<RepositoryFileChange> changes,
                RepositoryReconciliationResult result)
            {
                HashSet<string> matchedAddedPaths = [with(StringComparer.Ordinal)];

                foreach (RepoFile removed in removedCandidates)
                {
                    RepoFile match = null;

                    foreach (RepoFile added in addedCandidates)
                    {
                        if (matchedAddedPaths.Contains(added.RelativePath))
                        {
                            continue;
                        }

                        if (string.Equals(removed.Sha256, added.Sha256, StringComparison.Ordinal) && removed.FileSize == added.FileSize)
                        {
                            match = added;
                            break;
                        }
                    }

                    if (match is not null)
                    {
                        result.RenamedFiles++;
                        matchedAddedPaths.Add(match.RelativePath);
                        changes.Add(BuildRenameChange(repositoryId, match, removed.RelativePath));
                    }
                    else
                    {
                        result.RemovedFiles++;
                        changes.Add(BuildChange(repositoryId, removed, RepositoryChangeType.Removed));
                    }
                }

                return matchedAddedPaths;
            }

            private static RepositoryFileChange BuildChange(long repositoryId, RepoFile file, RepositoryChangeType changeType)
            {
                return new RepositoryFileChange
                {
                    RepositoryId = repositoryId,
                    FileId = file.Id,
                    RelativePath = file.RelativePath,
                    ChangeType = changeType,
                    Sha256 = file.Sha256,
                    FileSize = file.FileSize,
                    LastModifiedDate = file.LastModifiedDate
                };
            }

            private static RepositoryFileChange BuildRenameChange(long repositoryId, RepoFile file, string previousRelativePath)
            {
                return new RepositoryFileChange
                {
                    RepositoryId = repositoryId,
                    FileId = file.Id,
                    RelativePath = file.RelativePath,
                    PreviousRelativePath = previousRelativePath,
                    ChangeType = RepositoryChangeType.Renamed,
                    Sha256 = file.Sha256,
                    FileSize = file.FileSize,
                    LastModifiedDate = file.LastModifiedDate
                };
            }

            public Task<long> UpsertAsync(RepoFile file, CancellationToken cancellationToken = default)
            {
                (long RepositoryId, string RelativePath) key = (file.RepositoryId, file.RelativePath);

                if (_items.TryGetValue(key, out RepoFile existing))
                {
                    file.Id = existing.Id;
                }
                else if (file.Id == 0)
                {
                    file.Id = _nextId++;
                }

                _items[key] = file;
                return Task.FromResult(file.Id);
            }

            public Task UpdateAsync(RepoFile file, CancellationToken cancellationToken = default)
            {
                RepoFile existing = _items.Values.FirstOrDefault(f => f.Id == file.Id);

                if (existing is not null)
                {
                    _items[(existing.RepositoryId, existing.RelativePath)] = file;
                }

                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                RepoFile file = _items.Values.FirstOrDefault(f => f.Id == id);

                if (file is not null)
                {
                    _items.Remove((file.RepositoryId, file.RelativePath));
                }

                return Task.CompletedTask;
            }

            public Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                foreach ((long RepositoryId, string RelativePath) key in _items.Keys.Where(k => k.RepositoryId == repositoryId).ToList())
                {
                    _items.Remove(key);
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewTypeRepository : IReviewTypeRepository
        {
            private readonly Dictionary<long, ReviewType> _items = [];
            private long _nextId = 1;

            public void Add(ReviewType reviewType)
            {
                if (reviewType.Id == 0)
                {
                    reviewType.Id = _nextId++;
                }

                _items[reviewType.Id] = reviewType;
            }

            public Task<ReviewType> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue(id, out ReviewType reviewType);
                return Task.FromResult(reviewType);
            }

            public Task<IReadOnlyList<ReviewType>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. _items.Values.Where(t => t.RepositoryId == repositoryId)]);
            }

            public Task<IReadOnlyList<ReviewType>> GetEnabledByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. _items.Values.Where(t => t.RepositoryId == repositoryId && t.Enabled)]);
            }

            public Task<long> AddAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                Add(reviewType);
                return Task.FromResult(reviewType.Id);
            }

            public Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                _items[reviewType.Id] = reviewType;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.Remove(id);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewLinkRepository : IReviewLinkRepository
        {
            private readonly Dictionary<(long RepoFileId, long ReviewTypeId), ReviewLink> _items = [];
            private long _nextId = 1;

            public void Add(ReviewLink link)
            {
                if (link.Id == 0)
                {
                    link.Id = _nextId++;
                }

                _items[(link.RepoFileId, link.ReviewTypeId)] = link;
            }

            public Task<ReviewLink> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                ReviewLink link = _items.Values.FirstOrDefault(l => l.Id == id);
                return Task.FromResult(link);
            }

            public Task<ReviewLink> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue((repoFileId, reviewTypeId), out ReviewLink link);
                return Task.FromResult(link);
            }

            public Task<IReadOnlyList<ReviewLink>> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewLink>>([.. _items.Values.Where(l => l.RepoFileId == repoFileId)]);
            }

            public Task<IReadOnlyList<ReviewLink>> GetByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewLink>>([.. _items.Values.Where(l => l.ReviewTypeId == reviewTypeId)]);
            }

            public Task<long> UpsertAsync(ReviewLink link, CancellationToken cancellationToken = default)
            {
                (long RepoFileId, long ReviewTypeId) key = (link.RepoFileId, link.ReviewTypeId);

                if (_items.TryGetValue(key, out ReviewLink existing))
                {
                    link.Id = existing.Id;
                }
                else if (link.Id == 0)
                {
                    link.Id = _nextId++;
                }

                _items[key] = link;
                return Task.FromResult(link.Id);
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                ReviewLink link = _items.Values.FirstOrDefault(l => l.Id == id);

                if (link is not null)
                {
                    _items.Remove((link.RepoFileId, link.ReviewTypeId));
                }

                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                foreach ((long RepoFileId, long ReviewTypeId) key in _items.Keys.Where(k => k.RepoFileId == repoFileId).ToList())
                {
                    _items.Remove(key);
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepoFileAnalysisRepository : IRepoFileAnalysisRepository
        {
            private readonly Dictionary<(long RepoFileId, long ReviewTypeId), RepoFileAnalysis> _items = [];
            private long _nextId = 1;

            public void Add(RepoFileAnalysis analysis)
            {
                if (analysis.Id == 0)
                {
                    analysis.Id = _nextId++;
                }

                _items[(analysis.RepoFileId, analysis.ReviewTypeId)] = analysis;
            }

            public Task<RepoFileAnalysis> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue((repoFileId, reviewTypeId), out RepoFileAnalysis analysis);
                return Task.FromResult(analysis);
            }

            public Task<IReadOnlyList<RepoFileAnalysis>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepoFileAnalysis>>([.. _items.Values.Where(a => a.RepositoryId == repositoryId)]);
            }

            public Task<long> UpsertAsync(RepoFileAnalysis analysis, CancellationToken cancellationToken = default)
            {
                (long RepoFileId, long ReviewTypeId) key = (analysis.RepoFileId, analysis.ReviewTypeId);

                if (_items.TryGetValue(key, out RepoFileAnalysis existing))
                {
                    analysis.Id = existing.Id;
                }
                else if (analysis.Id == 0)
                {
                    analysis.Id = _nextId++;
                }

                _items[key] = analysis;
                return Task.FromResult(analysis.Id);
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                RepoFileAnalysis analysis = _items.Values.FirstOrDefault(a => a.Id == id);

                if (analysis is not null)
                {
                    _items.Remove((analysis.RepoFileId, analysis.ReviewTypeId));
                }

                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                foreach ((long RepoFileId, long ReviewTypeId) key in _items.Keys.Where(k => k.RepoFileId == repoFileId).ToList())
                {
                    _items.Remove(key);
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeQueueRepository : IQueueRepository
        {
            private readonly List<QueueItem> _items = [];
            private long _nextId = 1;

            public Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
            {
                bool duplicate = _items.Any(i => i.RepoFileId == item.RepoFileId && i.ReviewTypeId == item.ReviewTypeId);

                if (duplicate)
                {
                    return Task.FromResult(false);
                }

                item.Id = _nextId++;
                _items.Add(item);
                return Task.FromResult(true);
            }

            public Task<QueueItem> ClaimNextAsync(DateTime leasedUntilUtc, CancellationToken cancellationToken = default)
            {
                QueueItem item = _items
                    .Where(i => i.Status == QueueItemStatus.Pending && (i.NextAttemptUtc is null || i.NextAttemptUtc <= DateTime.UtcNow))
                    .OrderByDescending(i => i.Priority)
                    .ThenBy(i => i.CreatedDate)
                    .FirstOrDefault();

                if (item is not null)
                {
                    item.Status = QueueItemStatus.InFlight;
                    item.LeasedUntilUtc = leasedUntilUtc;
                    item.AttemptCount++;
                }

                return Task.FromResult(item);
            }

            public Task ReleaseForRetryAsync(long id, int priority, DateTime nextAttemptUtc, string lastError, CancellationToken cancellationToken = default)
            {
                QueueItem item = _items.FirstOrDefault(i => i.Id == id);
                if (item is not null)
                {
                    item.Status = QueueItemStatus.Pending;
                    item.Priority = priority;
                    item.NextAttemptUtc = nextAttemptUtc;
                    item.LastError = lastError ?? string.Empty;
                    item.LeasedUntilUtc = null;
                }

                return Task.CompletedTask;
            }

            public Task MarkCompletedAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.RemoveAll(i => i.Id == id);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<QueueItem>>([.. _items.Where(i => i.Status == QueueItemStatus.Pending).OrderByDescending(i => i.Priority).ThenBy(i => i.CreatedDate)]);
            }

            public Task<IReadOnlyList<QueueItem>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<QueueItem>>([.. _items.Where(i => i.RepositoryId == repositoryId)]);
            }

            public Task RemoveAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.RemoveAll(i => i.Id == id);
                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                _items.RemoveAll(i => i.RepoFileId == repoFileId);
                return Task.CompletedTask;
            }

            public Task<int> CountAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_items.Count(i => i.Status == QueueItemStatus.Pending));
            }

            public Task<QueueMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
            {
                List<QueueItem> pending = [.. _items.Where(i => i.Status == QueueItemStatus.Pending)];
                QueueMetrics metrics = new()
                {
                    PendingCount = pending.Count,
                    RetryCount = pending.Count(i => i.AttemptCount > 0),
                    OldestPendingAge = pending.Count == 0 ? (TimeSpan?)null : DateTime.UtcNow - pending.Min(i => i.CreatedDate)
                };

                return Task.FromResult(metrics);
            }
        }

        private sealed class FakeCSharpAnalysisEngine : ICSharpAnalysisEngine
        {
            public string Name => "Fake C# Analyser";

            public string Language => "CSharp";

            public IReadOnlyList<string> FilePatterns => [Consts.CSharpFilePattern];

            public bool RequiresAnalysisResult { get; set; } = true;

            public bool ThrowOnAnalyse { get; set; }

            public Task AnalyseAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public bool RequiresAnalysis(string repositoryName, string filePath)
            {
                return RequiresAnalysisResult;
            }

            public Task<string> AnalyseSymbolsAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default)
            {
                if (ThrowOnAnalyse)
                {
                    throw new InvalidOperationException("Simulated analysis failure.");
                }

                return Task.FromResult("{}");
            }
        }

        private sealed class FakeProjectDiscoveryService : IProjectDiscoveryService
        {
            public List<RepositoryProject> Projects { get; } = [];

            public Task<IReadOnlyList<RepositoryProject>> DiscoverAsync(string localPath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryProject>>(Projects);
            }
        }

        private sealed class FakeProjectRepository : IProjectRepository
        {
            private readonly List<RepositoryProject> _items = [];

            public int ReconcileCount { get; private set; }

            public Task<IReadOnlyList<RepositoryProject>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryProject>>([.. _items.Where(project => project.RepositoryId == repositoryId)]);
            }

            public Task ReconcileAsync(long repositoryId, IReadOnlyList<RepositoryProject> projects, CancellationToken cancellationToken = default)
            {
                ReconcileCount++;
                _items.Clear();
                _items.AddRange(projects);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepositoryScanRepository : IRepositoryScanRepository
        {
            public List<RepositoryScanRecord> Records { get; } = [];

            public Task<long> AddAsync(RepositoryScanRecord record, CancellationToken cancellationToken = default)
            {
                record.Id = Records.Count + 1;
                Records.Add(record);
                return Task.FromResult(record.Id);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanRecord>>([.. Records.Where(record => record.RepositoryId == repositoryId)]);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanRecord>>([.. Records]);
            }
        }

        private sealed class CountingFileHasher : IFileHasher
        {
            private readonly FileHasher _inner = new();

            public int AsyncCallCount { get; private set; }

            public string ComputeHash(string filePath)
            {
                return _inner.ComputeHash(filePath);
            }

            public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
            {
                AsyncCallCount++;
                return await _inner.ComputeHashAsync(filePath, cancellationToken);
            }
        }

        private sealed class FlakyScanner : IRepositoryScanner
        {
            private readonly RepositoryScanner _inner = new();
            private bool _failNext = true;

            public async Task<RepositoryScanResult> ScanAsync(
                string localPath,
                RepositoryScanOptions options = null,
                IProgress<RepositoryScanProgress> progress = null,
                CancellationToken cancellationToken = default,
                IReadOnlyDictionary<string, RepoFile> existingFiles = null,
                bool forceFullScan = false)
            {
                if (_failNext)
                {
                    _failNext = false;
                    throw new IOException("Simulated interruption.");
                }

                return await _inner.ScanAsync(localPath, options, progress, cancellationToken, existingFiles, forceFullScan);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_FailedReview_ReleasesForRetryWithDecayedPriority()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new() { Response = new ReviewResponse { Success = false, Error = "boom" } };
                FakeRetryPolicy retryPolicy = new() { Delay = TimeSpan.FromMinutes(7) };
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                options.DefaultRetry.MaxAttempts = 3;

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, retryPolicy, options);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                Assert.AreEqual(1, await queue.CountAsync(TestContext.CancellationToken));

                QueueItem pending = (await queue.GetPendingAsync(TestContext.CancellationToken))[0];
                Assert.AreEqual(QueueItemStatus.Pending, pending.Status);
                Assert.AreEqual(50, pending.Priority);
                Assert.AreEqual(1, pending.AttemptCount);
                Assert.AreEqual("boom", pending.LastError);
                Assert.IsGreaterThanOrEqualTo(DateTime.UtcNow.AddMinutes(6), pending.NextAttemptUtc.Value);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_FailedReview_PersistsUsageRecord()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new()
                {
                    Response = new ReviewResponse
                    {
                        Success = false,
                        Error = "boom",
                        EngineId = 7,
                        EngineName = "OpenAI",
                        EngineModel = "gpt-4o",
                        Usage = new TokenUsage
                        {
                            AttemptCount = 1,
                            PromptTokens = 12,
                            CompletionTokens = 3,
                            TotalTokens = 15,
                            CachedTokens = 4
                        }
                    }
                };

                FakeReviewUsageRepository usageRepository = new();
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                options.DefaultRetry.MaxAttempts = 3;

                ReviewProcessor processor = CreateReviewProcessor(
                    queue,
                    repositories,
                    files,
                    reviewTypes,
                    engine,
                    new FakeRetryPolicy(),
                    options,
                    usageRepository: usageRepository);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                Assert.HasCount(1, usageRepository.Added);
                Assert.AreEqual(1, usageRepository.Added[0].RepositoryId);
                Assert.AreEqual(1, usageRepository.Added[0].RepoFileId);
                Assert.AreEqual(1, usageRepository.Added[0].ReviewTypeId);
                Assert.AreEqual(7, usageRepository.Added[0].EngineId);
                Assert.AreEqual("OpenAI", usageRepository.Added[0].EngineName);
                Assert.AreEqual("gpt-4o", usageRepository.Added[0].EngineModel);
                Assert.AreEqual(12, usageRepository.Added[0].PromptTokens);
                Assert.AreEqual(3, usageRepository.Added[0].CompletionTokens);
                Assert.AreEqual(15, usageRepository.Added[0].TotalTokens);
                Assert.AreEqual(4, usageRepository.Added[0].CachedTokens);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_MaxAttemptsExhausted_RemovesItemAndRecordsOperationalFinding()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    MaxAttempts = 1,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new() { Response = new ReviewResponse { Success = false, Error = "boom" } };
                FakeFindingRepository findings = new();
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                options.DefaultRetry.MaxAttempts = 3;

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, findings);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                QueueMetrics metrics = await queue.GetMetricsAsync(TestContext.CancellationToken);
                Assert.AreEqual(0, metrics.PendingCount);
                Assert.AreEqual(0, await queue.CountAsync(TestContext.CancellationToken));
                Assert.HasCount(1, findings.Added);
                Assert.AreEqual(FindingCategory.OperationalFailure, findings.Added[0].Category);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_Success_MarksCompletedAndPersistsOutcome()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new() { Response = new ReviewResponse { Success = true, Results = [] } };
                FakeReviewOutcomeStore outcomes = new();
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, outcomes: outcomes);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                Assert.AreEqual(0, await queue.CountAsync(TestContext.CancellationToken));
                Assert.HasCount(1, outcomes.Outcomes);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_SeedsRelatedContextBeforeReview()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new() { Response = new ReviewResponse { Success = true, Results = [] } };
                FakeReviewContextProvider contextProvider = new()
                {
                    Response = new ReviewContextResponse
                    {
                        ContextKey = Consts.ContextKeyRelated,
                        Items =
                        [
                            new ContextItem
                            {
                                Title = "Demo.Consumer",
                                Content = "Demo.Program is used by Class Demo.Consumer (Consumer.cs:12)",
                                SourcePath = Path.Combine(root, "Consumer.cs")
                            }
                        ]
                    }
                };
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                FakeReviewOutcomeStore outcomes = new();

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, outcomes: outcomes, contextProvider: contextProvider);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                Assert.IsNotNull(engine.LastRequest);
                Assert.HasCount(1, engine.LastRequest.InitialContext);
                Assert.IsTrue(engine.LastRequest.InitialContext[0].Contains("is used by", StringComparison.Ordinal));
                Assert.HasCount(0, engine.LastRequest.ContextRounds);
                Assert.HasCount(1, contextProvider.Requests);
                Assert.AreEqual(Consts.ContextKeyRelated, contextProvider.Requests[0].ContextKey);
                Assert.HasCount(1, outcomes.Outcomes);
                Assert.IsNotNull(outcomes.Outcomes[0].Context);
                Assert.HasCount(1, outcomes.Outcomes[0].Context.ContextItems);
                Assert.IsFalse(outcomes.Outcomes[0].Context.ContextItems[0].Requested);
                Assert.HasCount(0, outcomes.Outcomes[0].Context.RequestedContextKeys);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_SearchRequest_PopulatesQuery()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new();
                engine.Responses.Add(new ReviewResponse { Success = true, ContextRequests = ["search:Program"] });
                engine.Responses.Add(new ReviewResponse { Success = true, Results = [] });

                FakeReviewContextProvider contextProvider = new();
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                FakeReviewOutcomeStore outcomes = new();

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, outcomes: outcomes, contextProvider: contextProvider);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                ReviewContextRequest searchRequest = contextProvider.Requests.FirstOrDefault(request => request.ContextKey == Consts.ContextKeySearch);
                Assert.IsNotNull(searchRequest);
                Assert.AreEqual("Program", searchRequest.Query);
                Assert.HasCount(1, outcomes.Outcomes);
                Assert.IsNotNull(outcomes.Outcomes[0].Context);
                Assert.HasCount(1, outcomes.Outcomes[0].Context.RequestedContextKeys);
                Assert.AreEqual("search:Program", outcomes.Outcomes[0].Context.RequestedContextKeys[0]);
                Assert.HasCount(0, outcomes.Outcomes[0].Context.ContextItems);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_FilesRequest_PopulatesQuery()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new();
                engine.Responses.Add(new ReviewResponse { Success = true, ContextRequests = ["files:Consumer.cs"] });
                engine.Responses.Add(new ReviewResponse { Success = true, Results = [] });

                FakeReviewContextProvider contextProvider = new();
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                FakeReviewOutcomeStore outcomes = new();

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, outcomes: outcomes, contextProvider: contextProvider);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                ReviewContextRequest filesRequest = contextProvider.Requests.FirstOrDefault(request => request.ContextKey == Consts.ContextKeyFiles);
                Assert.IsNotNull(filesRequest);
                Assert.AreEqual("Consumer.cs", filesRequest.Query);
                Assert.IsNotNull(engine.LastRequest);
                Assert.HasCount(1, engine.LastRequest.ContextRounds);
                Assert.HasCount(1, engine.LastRequest.ContextRounds[0].RequestedKeys);
                Assert.AreEqual("files:Consumer.cs", engine.LastRequest.ContextRounds[0].RequestedKeys[0]);
                Assert.HasCount(1, outcomes.Outcomes);
                Assert.IsNotNull(outcomes.Outcomes[0].Context);
                Assert.HasCount(1, outcomes.Outcomes[0].Context.RequestedContextKeys);
                Assert.AreEqual("files:Consumer.cs", outcomes.Outcomes[0].Context.RequestedContextKeys[0]);
                Assert.HasCount(0, outcomes.Outcomes[0].Context.ContextItems);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ReviewProcessor_RequestedContext_MarksItemsAsRequested()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                FakeRepositoryRepository repositories = new();
                repositories.Add(new Repository { Id = 1, Name = "Repo", LocalPath = root, Enabled = true });

                FakeRepoFileRepository files = new();
                files.Add(new RepoFile { Id = 1, RepositoryId = 1, RelativePath = "Program.cs", FileName = "Program.cs", Extension = "cs", Sha256 = "hash" });

                FakeReviewTypeRepository reviewTypes = new();
                reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 10, Enabled = true, Prompt = "prompt" });

                FakeQueueRepository queue = new();
                await queue.EnqueueAsync(new QueueItem
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    ReviewTypeId = 1,
                    Priority = 100,
                    CreatedDate = DateTime.UtcNow
                }, TestContext.CancellationToken);

                FakeAIReviewEngine engine = new();
                engine.Responses.Add(new ReviewResponse { Success = true, ContextRequests = ["search:Program"] });
                engine.Responses.Add(new ReviewResponse { Success = true, Results = [] });

                FakeReviewContextProvider contextProvider = new()
                {
                    Response = new ReviewContextResponse
                    {
                        ContextKey = Consts.ContextKeyRelated,
                        Items =
                        [
                            new ContextItem
                            {
                                Title = "Program",
                                Content = "class Program { }",
                                SourcePath = Path.Combine(root, "Program.cs")
                            }
                        ]
                    }
                };
                ResilienceOptions options = new() { QueuePriorityDecayFactor = 2, QueueMinimumPriority = 1 };
                FakeReviewOutcomeStore outcomes = new();

                ReviewProcessor processor = CreateReviewProcessor(queue, repositories, files, reviewTypes, engine, new FakeRetryPolicy(), options, outcomes: outcomes, contextProvider: contextProvider);

                bool processed = await processor.ProcessNextAsync(TestContext.CancellationToken);

                Assert.IsTrue(processed);
                Assert.HasCount(1, outcomes.Outcomes);
                Assert.IsNotNull(outcomes.Outcomes[0].Context);
                Assert.HasCount(2, outcomes.Outcomes[0].Context.ContextItems);
                Assert.IsFalse(outcomes.Outcomes[0].Context.ContextItems[0].Requested);
                Assert.IsTrue(outcomes.Outcomes[0].Context.ContextItems[1].Requested);
                Assert.HasCount(1, outcomes.Outcomes[0].Context.RequestedContextKeys);
                Assert.AreEqual("search:Program", outcomes.Outcomes[0].Context.RequestedContextKeys[0]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static ReviewProcessor CreateReviewProcessor(
            FakeQueueRepository queue,
            FakeRepositoryRepository repositories,
            FakeRepoFileRepository files,
            FakeReviewTypeRepository reviewTypes,
            FakeAIReviewEngine engine,
            IRetryPolicy retryPolicy,
            ResilienceOptions options,
            FakeFindingRepository findings = null,
            FakeReviewOutcomeStore outcomes = null,
            FakeReviewUsageRepository usageRepository = null,
            FakeReviewContextProvider contextProvider = null,
            IArchitectureSummaryService architectureSummaryService = null)
        {
            return new ReviewProcessor(
                new ReviewQueue(queue),
                repositories,
                files,
                reviewTypes,
                outcomes ?? new FakeReviewOutcomeStore(),
                findings ?? new FakeFindingRepository(),
                new FakePromptResolutionService(),
                engine,
                contextProvider ?? new FakeReviewContextProvider(),
                new FakeRetryPolicyProvider(retryPolicy),
                usageRepository ?? new FakeReviewUsageRepository(),
                new FakeDuplicateDetectionQueue(),
                new IssueSettingsProvider(),
                new SeverityRuleEngine(new SeverityRuleProvider()),
                architectureSummaryService ?? new FakeArchitectureSummaryService(),
                options);
        }

        private sealed class FakeDuplicateDetectionQueue : IDuplicateDetectionQueue
        {
            public List<(long RepositoryId, long FindingId)> Enqueued { get; } = [];

            public Task<long> EnqueueAsync(long repositoryId, long findingId, CancellationToken cancellationToken = default)
            {
                Enqueued.Add((repositoryId, findingId));
                return Task.FromResult((long)Enqueued.Count);
            }

            public Task<DuplicateDetectionQueueItem> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DuplicateDetectionQueueItem>(null);
            }

            public Task CompleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task FailAsync(long id, string errorMessage, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string errorMessage, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAIReviewEngine : IAIReviewEngine
        {
            public ReviewResponse Response { get; set; } = new();

            public List<ReviewResponse> Responses { get; } = [];

            public ReviewRequest LastRequest { get; private set; }

            public int CallCount { get; private set; }

            public Task<ReviewResponse> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                ReviewResponse response = CallCount < Responses.Count ? Responses[CallCount] : Response;
                CallCount++;
                return Task.FromResult(response);
            }
        }

        private sealed class FakeArchitectureSummaryService : IArchitectureSummaryService
        {
            public string Summary { get; set; } = string.Empty;

            public int CallCount { get; private set; }

            public Task<string> GetOrCreateAsync(RepoFile file, string fileContent, string language, CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(Summary);
            }
        }

        private sealed class FakeReviewOutcomeStore : IReviewOutcomeStore
        {
            public List<ReviewOutcome> Outcomes { get; } = [];

            public Task PersistAsync(ReviewOutcome outcome, CancellationToken cancellationToken = default)
            {
                Outcomes.Add(outcome);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewUsageRepository : IReviewUsageRepository
        {
            public List<ReviewUsage> Added { get; } = [];

            public Task<long> AddAsync(ReviewUsage usage, CancellationToken cancellationToken = default)
            {
                Added.Add(usage);
                return Task.FromResult((long)Added.Count);
            }

            public Task<IReadOnlyList<ReviewUsage>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewUsage>>(Added);
            }

            public Task<ReviewUsageSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReviewUsageSummary());
            }
        }

        private sealed class FakeFindingRepository : IFindingRepository
        {
            public List<Finding> Added { get; } = [];

            public Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Finding>(null);
            }

            public Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task<Finding> GetByFingerprintAsync(long repositoryId, string fingerprint, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Finding>(null);
            }

            public Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FindingQueryResult());
            }

            public Task<FindingNavigation> GetNavigationAsync(long findingId, FindingQuery query, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FindingNavigation());
            }

            public Task<IReadOnlyList<RepositoryIssueCount>> CountByRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryIssueCount>>([]);
            }

            public Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Added.Count(finding => finding.Category == FindingCategory.OperationalFailure));
            }

            public Task<IReadOnlyList<Finding>> GetSimilarityCandidatesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task MarkDuplicateAsync(long findingId, long duplicateOfFindingId, double similarityScore, DateTime lastModifiedUtc, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                finding.Id = Added.Count + 1;
                Added.Add(finding);
                return Task.FromResult(finding.Id);
            }

            public Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakePromptResolutionService : IPromptResolutionService
        {
            public ResolvedPrompts ResolvedPrompts { get; set; } = new();

            public Task<ResolvedPrompts> ResolveForDetectionAsync(long reviewTypeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(ResolvedPrompts);
            }

            public Task<PromptVersion> GetActiveSystemVersionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<PromptVersion>(null);
            }

            public Task<PromptVersion> GetActiveReviewVersionAsync(long reviewTypeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<PromptVersion>(null);
            }

            public Task SeedFromConfigurationAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewContextProvider : IReviewContextProvider
        {
            public ReviewContextResponse Response { get; set; } = new();

            public List<ReviewContextRequest> Requests { get; } = [];

            public Task<ReviewContextResponse> GetContextAsync(ReviewContextRequest request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                return Task.FromResult(Response);
            }
        }

        private sealed class FakeRetryPolicy : IRetryPolicy
        {
            public TimeSpan Delay { get; set; } = TimeSpan.FromMinutes(5);

            public RetryDecision Evaluate(int attempt, Exception exception)
            {
                return RetryDecision.Retry(Delay, FailureCategory.Transient);
            }

            public TimeSpan GetRetryDelay(int attempt)
            {
                return Delay;
            }
        }

        private sealed class FakeRetryPolicyProvider : IRetryPolicyProvider
        {
            private readonly IRetryPolicy _policy;

            public FakeRetryPolicyProvider(IRetryPolicy policy)
            {
                _policy = policy;
            }

            public IRetryPolicy GetPolicy(RetryOperationType operationType)
            {
                return _policy;
            }
        }

        public TestContext TestContext { get; set; }
    }
}
