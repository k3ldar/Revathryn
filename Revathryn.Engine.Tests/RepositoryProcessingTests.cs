using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
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
                RepositoryScanResult scan = await scanner.ScanAsync(root);

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
                RepositoryScanResult scan = await scanner.ScanAsync(root);

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
                RepositoryScanResult scan = await scanner.ScanAsync(root, options);

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
                RepositoryScanResult first = await scanner.ScanAsync(root);
                RepositoryScanResult second = await scanner.ScanAsync(root);

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
                RepositoryScanResult first = await scanner.ScanAsync(root);

                File.WriteAllText(filePath, "class Program { void M() { } }");
                RepositoryScanResult second = await scanner.ScanAsync(root);

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
                RepositoryScanResult first = await scanner.ScanAsync(root);

                File.WriteAllText(filePath, "class Program { void M() { } }");
                File.SetLastWriteTimeUtc(filePath, originalTimestamp);

                RepositoryScanResult second = await scanner.ScanAsync(root);

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

                using (FileStream locked = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    RepositoryScanner scanner = new();
                    RepositoryScanResult scan = await scanner.ScanAsync(root);

                    Assert.HasCount(0, scan.DiscoveredFiles);
                    Assert.HasCount(1, scan.Failures);
                    Assert.AreEqual("Locked.cs", scan.Failures[0].RelativePath);
                }
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
            RepositoryService service = new(repositories, reviewTypes);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = "  " }));
        }

        [TestMethod]
        public async Task RepositoryService_RejectsNonexistentDirectory()
        {
            string missing = Path.Combine(CreateTempDirectory(), "does-not-exist");

            FakeRepositoryRepository repositories = new();
            FakeReviewTypeRepository reviewTypes = new();
            RepositoryService service = new(repositories, reviewTypes);

            await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = missing }));
        }

        [TestMethod]
        public async Task RepositoryService_RejectsDuplicateRepository()
        {
            string root = CreateTempDirectory();
            try
            {
                FakeRepositoryRepository repositories = new();
                FakeReviewTypeRepository reviewTypes = new();
                RepositoryService service = new(repositories, reviewTypes);

                await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root });

                string duplicatePath = root + Path.DirectorySeparatorChar;

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RegisterAsync(new Repository { Name = "Repo Again", LocalPath = duplicatePath }));
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
                RepositoryService service = new(repositories, reviewTypes);

                Repository registered = await service.RegisterAsync(new Repository
                {
                    Name = "Repo",
                    LocalPath = root + Path.DirectorySeparatorChar
                });

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
                RepositoryService service = new(repositories, reviewTypes);

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root });

                await service.SetEnabledAsync(registered.Id, false);

                Repository disabled = await service.GetAsync(registered.Id);
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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(1, result.Added);
            Assert.HasCount(1, await harness.Queue.GetPendingAsync());
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioB_ChangedFileQueuesReview()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.SeedAnalysis(1, 1, "old-hash", "v1");

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync());
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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(1, result.Added);
            IReadOnlyList<QueueItem> pending = await harness.Queue.GetPendingAsync();
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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(1, result.Added);
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioF_DisabledReviewTypeIgnored()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1", enabled: false);

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync());
        }

        [TestMethod]
        public async Task ReviewScheduler_ScenarioG_DisabledLinkIgnored()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");
            harness.Links.Add(new ReviewLink { RepoFileId = 1, ReviewTypeId = 1, Enabled = false });

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

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

            await harness.Scheduler.ScheduleRepositoryAsync(1);

            QueueItem item = await harness.Queue.DequeueAsync();
            Assert.AreEqual(500, item.Priority);
        }

        [TestMethod]
        public async Task ReviewScheduler_RepeatedScheduleDoesNotDuplicate()
        {
            SchedulerHarness harness = new();
            harness.SeedRepository();
            harness.SeedFile(1, "src/A.cs", "cs", "h1");
            harness.SeedReviewType(1, "cs", "v1");

            ReviewScheduleResult first = await harness.Scheduler.ScheduleRepositoryAsync(1);
            ReviewScheduleResult second = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(1, first.Added);
            Assert.AreEqual(0, second.Added);
            Assert.AreEqual(1, second.DuplicatesSkipped);
            Assert.AreEqual(1, await harness.Queue.CountAsync());
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
                Assert.AreEqual(1, await harness.Queue.CountAsync());
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
                Assert.AreEqual(1, await harness.Queue.CountAsync());
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
                IReadOnlyList<RepoFile> remaining = await harness.Files.GetByRepositoryAsync(1);
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
                RepositoryService service = new(repositories, reviewTypes);

                await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => service.RegisterAsync(new Repository { Name = "Repo", LocalPath = filePath }));
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
                RepositoryService service = new(repositories, reviewTypes);

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root, Description = "Before" });

                registered.Description = "After";
                registered.ScanOptions = new RepositoryScanOptions { RespectGitIgnore = true };
                await service.UpdateAsync(registered);

                Repository updated = await service.GetAsync(registered.Id);

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
                RepositoryService service = new(repositories, reviewTypes);

                Repository registered = await service.RegisterAsync(new Repository { Name = "Repo", LocalPath = root });
                await service.DeleteAsync(registered.Id);

                Assert.IsNull(await service.GetAsync(registered.Id));
                Assert.IsTrue(Directory.Exists(root));
                Assert.IsTrue(File.Exists(Path.Combine(root, "keep.cs")));
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

            ReviewScheduleResult result = await harness.Scheduler.ScheduleRepositoryAsync(1);

            Assert.AreEqual(0, result.Added);
            Assert.AreEqual(0, await harness.Queue.CountAsync());
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

                RepoFile file = await harness.Files.GetByRelativePathAsync(1, "Program.cs");
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
                Assert.IsTrue(scans.Records[1].Succeeded);
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
            private readonly Dictionary<long, Repository> _items = new();
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
            private readonly Dictionary<(long RepositoryId, string RelativePath), RepoFile> _items = new();
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
                HashSet<string> discoveredPaths = new(StringComparer.Ordinal);
                HashSet<string> failedSet = failedPaths is null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(failedPaths, StringComparer.Ordinal);

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
                        }
                        else
                        {
                            result.ChangedFiles++;
                        }
                    }
                    else
                    {
                        if (file.Id == 0)
                        {
                            file.Id = _nextId++;
                        }

                        result.NewFiles++;
                    }

                    _items[key] = file;
                }

                foreach (RepoFile existing in _items.Values.Where(f => f.RepositoryId == repositoryId).ToList())
                {
                    if (discoveredPaths.Contains(existing.RelativePath) || failedSet.Contains(existing.RelativePath) || existing.IsRemoved)
                    {
                        continue;
                    }

                    result.RemovedFiles++;
                    existing.IsRemoved = true;
                }

                return Task.FromResult(result);
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
            private readonly Dictionary<long, ReviewType> _items = new();
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
            private readonly Dictionary<(long RepoFileId, long ReviewTypeId), ReviewLink> _items = new();
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
            private readonly Dictionary<(long RepoFileId, long ReviewTypeId), RepoFileAnalysis> _items = new();
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
            private readonly List<QueueItem> _items = new();
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

            public Task<QueueItem> DequeueAsync(CancellationToken cancellationToken = default)
            {
                QueueItem item = _items.OrderByDescending(i => i.Priority).ThenBy(i => i.CreatedDate).FirstOrDefault();

                if (item is not null)
                {
                    _items.Remove(item);
                }

                return Task.FromResult(item);
            }

            public Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<QueueItem>>([.. _items.OrderByDescending(i => i.Priority).ThenBy(i => i.CreatedDate)]);
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
                return Task.FromResult(_items.Count);
            }
        }

        private sealed class FakeCSharpAnalysisEngine : ICSharpAnalysisEngine
        {
            public string Name => "Fake C# Analyser";

            public string Language => "CSharp";

            public IReadOnlyList<string> FilePatterns => [Consts.CSharpFilePattern];

            public bool RequiresAnalysisResult { get; set; } = true;

            public bool ThrowOnAnalyse { get; set; }

            public Task AnalyseAsync(string filePath, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public bool RequiresAnalysis(string filePath)
            {
                return RequiresAnalysisResult;
            }

            public Task<string> AnalyseSymbolsAsync(string filePath, CancellationToken cancellationToken = default)
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
            public List<RepositoryProject> Projects { get; } = new();

            public Task<IReadOnlyList<RepositoryProject>> DiscoverAsync(string localPath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryProject>>(Projects);
            }
        }

        private sealed class FakeProjectRepository : IProjectRepository
        {
            private readonly List<RepositoryProject> _items = new();

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
            public List<RepositoryScanRecord> Records { get; } = new();

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
        }

        private sealed class FlakyScanner : IRepositoryScanner
        {
            private readonly RepositoryScanner _inner = new();
            private bool _failNext = true;

            public async Task<RepositoryScanResult> ScanAsync(
                string localPath,
                RepositoryScanOptions options = null,
                IProgress<RepositoryScanProgress> progress = null,
                CancellationToken cancellationToken = default)
            {
                if (_failNext)
                {
                    _failNext = false;
                    throw new IOException("Simulated interruption.");
                }

                return await _inner.ScanAsync(localPath, options, progress, cancellationToken);
            }
        }
    }
}
