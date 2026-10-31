using System.Diagnostics.CodeAnalysis;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SqliteDataTests
    {
        [TestMethod]
        public void DatabaseInitializer_CreatesAllTables()
        {
            using TestDatabase database = new();

            using SqliteConnection connection = database.Factory.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Repositories', 'RepoFiles', 'ReviewTypes', 'ReviewLinks', 'RepoFileAnalysis', 'Queue', 'Findings', 'AIEngines', 'AISettings', 'RepoProjects', 'RepositoryScans');";

            object result = command.ExecuteScalar();

            Assert.AreEqual(11, Convert.ToInt32(result));
        }

        [TestMethod]
        public async Task RepositoryRepository_PersistsAndReturnsRepository()
        {
            using TestDatabase database = new();
            RepositoryRepository repository = new(database.Factory);

            DateTime now = DateTime.UtcNow;
            long id = await repository.AddAsync(new Repository
            {
                Name = "Example",
                Description = "A sample repository",
                LocalPath = "C:\\src\\example",
                Enabled = true,
                AutomaticReviewEnabled = true,
                MaxContextRequests = 3,
                CreatedDate = now,
                LastModifiedDate = now
            });

            Repository loaded = await repository.GetByIdAsync(id);

            Assert.AreEqual(id, loaded.Id);
            Assert.AreEqual("Example", loaded.Name);
            Assert.AreEqual("C:\\src\\example", loaded.LocalPath);
            Assert.IsTrue(loaded.Enabled);
            Assert.IsTrue(loaded.AutomaticReviewEnabled);
            Assert.AreEqual(3, loaded.MaxContextRequests);
        }

        [TestMethod]
        public async Task RepoFileRepository_Upsert_DeduplicatesByRepositoryAndRelativePath()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            long firstId = await files.UpsertAsync(new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = "src/Program.cs",
                FileName = "Program.cs",
                Extension = "cs",
                Sha256 = "aaa",
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            });

            long secondId = await files.UpsertAsync(new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = "src/Program.cs",
                FileName = "Program.cs",
                Extension = "cs",
                Sha256 = "bbb",
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            });

            Assert.AreEqual(firstId, secondId);

            IReadOnlyList<RepoFile> all = await files.GetByRepositoryAsync(repositoryId);
            Assert.HasCount(1, all);
            Assert.AreEqual("bbb", all[0].Sha256);
        }

        [TestMethod]
        public async Task QueueRepository_PreventsDuplicatePendingItems()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"));

            QueueItem item = CreateQueueItem(repositoryId, fileId, reviewTypeId, 10);

            bool first = await queue.EnqueueAsync(item);
            bool second = await queue.EnqueueAsync(item);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, await queue.CountAsync());
        }

        [TestMethod]
        public async Task QueueRepository_DequeuesHighestPriorityFirst()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileA = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));
            long fileB = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/B.cs"));
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"));

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileA, reviewTypeId, 1));
            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileB, reviewTypeId, 50));

            QueueItem dequeued = await queue.DequeueAsync();

            Assert.AreEqual(fileB, dequeued.RepoFileId);
            Assert.AreEqual(50, dequeued.Priority);
            Assert.AreEqual(1, await queue.CountAsync());
        }

        [TestMethod]
        public async Task FindingRepository_TracksFingerprintsForDuplicateDetection()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            Finding finding = new()
            {
                RepositoryId = repositoryId,
                Fingerprint = "fp-001",
                Severity = FindingSeverity.High,
                Title = "Issue",
                Description = "Description",
                Status = FindingStatus.New,
                Category = FindingCategory.CodeIssue,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            long id = await findings.AddAsync(finding);

            Finding byFingerprint = await findings.GetByFingerprintAsync(repositoryId, "fp-001");

            Assert.AreEqual(id, byFingerprint.Id);
            Assert.AreEqual(FindingSeverity.High, byFingerprint.Severity);
        }

        [TestMethod]
        public async Task RepoFileAnalysisRepository_TracksFingerprintPerReviewType()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            RepoFileAnalysisRepository analysis = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"));

            RepoFileAnalysis record = new()
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash-1",
                LastAnalysedDate = DateTime.UtcNow,
                AnalysisVersion = "1.0"
            };

            long firstId = await analysis.UpsertAsync(record);
            record.Sha256 = "hash-2";
            long secondId = await analysis.UpsertAsync(record);

            Assert.AreEqual(firstId, secondId);

            RepoFileAnalysis loaded = await analysis.GetAsync(fileId, reviewTypeId);
            Assert.AreEqual("hash-2", loaded.Sha256);
        }

        [TestMethod]
        public async Task FindingRepository_DuplicateFingerprint_ReturnsExistingId()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            Finding finding = CreateFinding(repositoryId, "dup-fp");

            long firstId = await findings.AddAsync(finding);
            long secondId = await findings.AddAsync(CreateFinding(repositoryId, "dup-fp"));

            Assert.AreEqual(firstId, secondId);
            Assert.HasCount(1, await findings.GetByRepositoryAsync(repositoryId));
        }

        [TestMethod]
        public async Task Finding_RepoFileDeletion_SetsRepoFileIdToNull()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));

            Finding finding = CreateFinding(repositoryId, "fp-file");
            finding.RepoFileId = fileId;
            long findingId = await findings.AddAsync(finding);

            await files.DeleteAsync(fileId);

            Finding retained = await findings.GetByIdAsync(findingId);
            Assert.IsNotNull(retained);
            Assert.IsNull(retained.RepoFileId);
        }

        [TestMethod]
        public async Task RepoFileDeletion_CascadesDependentRecordsAndPreservesFindings()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            ReviewLinkRepository links = new(database.Factory);
            RepoFileAnalysisRepository analyses = new(database.Factory);
            QueueRepository queue = new(database.Factory);
            FindingRepository findings = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"));

            await links.UpsertAsync(new ReviewLink
            {
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Enabled = true
            });

            await analyses.UpsertAsync(new RepoFileAnalysis
            {
                RepositoryId = repositoryId,
                RepoFileId = fileId,
                ReviewTypeId = reviewTypeId,
                Sha256 = "hash",
                LastAnalysedDate = DateTime.UtcNow,
                AnalysisVersion = "1",
                ReviewDefinitionVersion = "v1"
            });

            await queue.EnqueueAsync(CreateQueueItem(repositoryId, fileId, reviewTypeId, 10));

            Finding finding = CreateFinding(repositoryId, "fp-cascade");
            finding.RepoFileId = fileId;
            long findingId = await findings.AddAsync(finding);

            await files.DeleteAsync(fileId);

            Assert.IsNull(await links.GetAsync(fileId, reviewTypeId));
            Assert.IsNull(await analyses.GetAsync(fileId, reviewTypeId));
            Assert.AreEqual(0, await queue.CountAsync());

            Finding retained = await findings.GetByIdAsync(findingId);
            Assert.IsNotNull(retained);
            Assert.IsNull(retained.RepoFileId);
        }

        [TestMethod]
        public async Task QueueRepository_ConcurrentInsertionIsSafe()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);
            ReviewTypeRepository reviewTypes = new(database.Factory);
            QueueRepository queue = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));
            long fileId = await files.UpsertAsync(CreateRepoFile(repositoryId, "src/A.cs"));
            long reviewTypeId = await reviewTypes.AddAsync(CreateReviewType(repositoryId, "C# Review"));

            QueueItem item = CreateQueueItem(repositoryId, fileId, reviewTypeId, 10);

            Task<bool>[] attempts =
            [
                queue.EnqueueAsync(item),
                queue.EnqueueAsync(item),
                queue.EnqueueAsync(item),
                queue.EnqueueAsync(item),
                queue.EnqueueAsync(item),
                queue.EnqueueAsync(item)
            ];

            bool[] results = await Task.WhenAll(attempts);

            Assert.HasCount(1, results.Where(r => r));
            Assert.AreEqual(1, await queue.CountAsync());
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_TracksNewChangedUnchangedAndRemoved()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";
            RepoFile fileB = CreateRepoFile(repositoryId, "src/B.cs");
            fileB.Sha256 = "hash-b";

            RepositoryReconciliationResult first = await files.ReconcileAsync(repositoryId, [fileA, fileB], null);

            Assert.AreEqual(2, first.NewFiles);
            Assert.AreEqual(0, first.ChangedFiles);
            Assert.AreEqual(0, first.UnchangedFiles);
            Assert.AreEqual(0, first.RemovedFiles);

            RepoFile fileAUnchanged = CreateRepoFile(repositoryId, "src/A.cs");
            fileAUnchanged.Sha256 = "hash-a";
            RepoFile fileBChanged = CreateRepoFile(repositoryId, "src/B.cs");
            fileBChanged.Sha256 = "hash-b2";
            RepoFile fileC = CreateRepoFile(repositoryId, "src/C.cs");
            fileC.Sha256 = "hash-c";

            RepositoryReconciliationResult second = await files.ReconcileAsync(repositoryId, [fileAUnchanged, fileBChanged, fileC], null);

            Assert.AreEqual(1, second.NewFiles);
            Assert.AreEqual(1, second.ChangedFiles);
            Assert.AreEqual(1, second.UnchangedFiles);
            Assert.AreEqual(0, second.RemovedFiles);

            RepositoryReconciliationResult third = await files.ReconcileAsync(repositoryId, [], null);

            Assert.AreEqual(3, third.RemovedFiles);
            Assert.AreEqual(0, third.NewFiles);
            Assert.AreEqual(0, third.ChangedFiles);
            Assert.AreEqual(0, third.UnchangedFiles);

            RepoFile removed = await files.GetByRelativePathAsync(repositoryId, "src/A.cs");
            Assert.IsTrue(removed.IsRemoved);
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_PreservesFailedPaths()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";

            await files.ReconcileAsync(repositoryId, [fileA], null);

            RepositoryReconciliationResult result = await files.ReconcileAsync(repositoryId, [], ["src/A.cs"]);

            Assert.AreEqual(0, result.RemovedFiles);

            RepoFile retained = await files.GetByRelativePathAsync(repositoryId, "src/A.cs");
            Assert.IsFalse(retained.IsRemoved);
        }

        [TestMethod]
        public async Task RepoFileRepository_Reconcile_ReintroducedFile_RestoresInventory()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepoFileRepository files = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            RepoFile fileA = CreateRepoFile(repositoryId, "src/A.cs");
            fileA.Sha256 = "hash-a";

            await files.ReconcileAsync(repositoryId, [fileA], null);
            await files.ReconcileAsync(repositoryId, [], null);

            RepoFile removed = await files.GetByRelativePathAsync(repositoryId, "src/A.cs");
            Assert.IsTrue(removed.IsRemoved);

            RepoFile reintroduced = CreateRepoFile(repositoryId, "src/A.cs");
            reintroduced.Sha256 = "hash-a";

            RepositoryReconciliationResult result = await files.ReconcileAsync(repositoryId, [reintroduced], null);

            Assert.AreEqual(1, result.UnchangedFiles);
            Assert.AreEqual(0, result.RemovedFiles);

            RepoFile restored = await files.GetByRelativePathAsync(repositoryId, "src/A.cs");
            Assert.IsFalse(restored.IsRemoved);
        }

        [TestMethod]
        public async Task ProjectRepository_Reconcile_LinksProjectsToSolution()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            ProjectRepository projects = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            RepositoryProject solution = new() { Name = "Revathryn", Kind = ProjectKind.Solution, RelativePath = "Revathryn.sln" };
            RepositoryProject app = new() { Name = "App", Kind = ProjectKind.Project, RelativePath = "src/App/App.csproj", DirectoryPath = "src/App", SolutionPath = "Revathryn.sln" };
            RepositoryProject nested = new() { Name = "Nested", Kind = ProjectKind.Project, RelativePath = "src/Nested/Nested.csproj", DirectoryPath = "src/Nested", SolutionPath = "Revathryn.sln" };

            await projects.ReconcileAsync(repositoryId, [solution, app, nested]);

            IReadOnlyList<RepositoryProject> loaded = await projects.GetByRepositoryAsync(repositoryId);

            Assert.HasCount(3, loaded);

            RepositoryProject loadedSolution = loaded.Single(p => p.Kind == ProjectKind.Solution);
            RepositoryProject loadedApp = loaded.Single(p => p.Name == "App");
            RepositoryProject loadedNested = loaded.Single(p => p.Name == "Nested");

            Assert.AreEqual(loadedSolution.Id, loadedApp.SolutionId);
            Assert.AreEqual(loadedSolution.Id, loadedNested.SolutionId);
        }

        [TestMethod]
        public async Task ProjectRepository_Reconcile_RemovesMissingProjects()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            ProjectRepository projects = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            RepositoryProject solution = new() { Name = "Revathryn", Kind = ProjectKind.Solution, RelativePath = "Revathryn.sln" };
            RepositoryProject app = new() { Name = "App", Kind = ProjectKind.Project, RelativePath = "src/App/App.csproj", DirectoryPath = "src/App", SolutionPath = "Revathryn.sln" };

            await projects.ReconcileAsync(repositoryId, [solution, app]);
            await projects.ReconcileAsync(repositoryId, [solution]);

            IReadOnlyList<RepositoryProject> remaining = await projects.GetByRepositoryAsync(repositoryId);

            Assert.HasCount(1, remaining);
            Assert.AreEqual(ProjectKind.Solution, remaining[0].Kind);
        }

        [TestMethod]
        public async Task RepositoryScanRepository_PersistsRecords_NewestFirst()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);
            RepositoryScanRepository scans = new(database.Factory);

            long repositoryId = await repositories.AddAsync(CreateRepository("Repo"));

            DateTime older = DateTime.UtcNow.AddMinutes(-5);
            DateTime newer = DateTime.UtcNow;

            long olderId = await scans.AddAsync(new RepositoryScanRecord
            {
                RepositoryId = repositoryId,
                StartedUtc = older,
                CompletedUtc = older.AddSeconds(1),
                Succeeded = true,
                FilesDiscovered = 1,
                NewFiles = 1
            });

            long newerId = await scans.AddAsync(new RepositoryScanRecord
            {
                RepositoryId = repositoryId,
                StartedUtc = newer,
                CompletedUtc = newer.AddSeconds(1),
                Succeeded = false
            });

            IReadOnlyList<RepositoryScanRecord> records = await scans.GetByRepositoryAsync(repositoryId);

            Assert.HasCount(2, records);
            Assert.AreEqual(newerId, records[0].Id);
            Assert.AreEqual(olderId, records[1].Id);
            Assert.IsFalse(records[0].Succeeded);
            Assert.IsTrue(records[1].Succeeded);
        }

        [TestMethod]
        public async Task RepositoryRepository_RoundTripsScanOptionsAndLastSuccessfulScanDate()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);

            DateTime now = DateTime.UtcNow;
            RepositoryScanOptions options = new()
            {
                RespectGitIgnore = true,
                IncludedExtensions = ["cs", "json"],
                ExcludedDirectoryNames = ["docs"]
            };

            long id = await repositories.AddAsync(new Repository
            {
                Name = "Repo",
                LocalPath = "C:\\src\\Repo",
                Enabled = true,
                ScanOptions = options,
                LastSuccessfulScanDate = now,
                CreatedDate = now,
                LastModifiedDate = now
            });

            Repository loaded = await repositories.GetByIdAsync(id);

            Assert.IsNotNull(loaded.ScanOptions);
            Assert.IsTrue(loaded.ScanOptions.RespectGitIgnore);
            CollectionAssert.AreEquivalent(new[] { "cs", "json" }, loaded.ScanOptions.IncludedExtensions.ToArray());
            CollectionAssert.AreEquivalent(new[] { "docs" }, loaded.ScanOptions.ExcludedDirectoryNames.ToArray());
            Assert.IsNotNull(loaded.LastSuccessfulScanDate);
            Assert.AreEqual(now, loaded.LastSuccessfulScanDate.Value);
        }

        [TestMethod]
        public async Task RepositoryRepository_NullScanState_RoundTripsAsNull()
        {
            using TestDatabase database = new();
            RepositoryRepository repositories = new(database.Factory);

            long id = await repositories.AddAsync(CreateRepository("Repo"));

            Repository loaded = await repositories.GetByIdAsync(id);

            Assert.IsNull(loaded.ScanOptions);
            Assert.IsNull(loaded.LastSuccessfulScanDate);
        }

        private static Repository CreateRepository(string name)
        {
            DateTime now = DateTime.UtcNow;

            return new Repository
            {
                Name = name,
                LocalPath = $"C:\\src\\{name}",
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private static RepoFile CreateRepoFile(long repositoryId, string relativePath)
        {
            return new RepoFile
            {
                RepositoryId = repositoryId,
                RelativePath = relativePath,
                FileName = Path.GetFileName(relativePath),
                Extension = Path.GetExtension(relativePath).TrimStart('.'),
                Sha256 = Guid.NewGuid().ToString("N"),
                LastModifiedDate = DateTime.UtcNow,
                LastDiscoveredDate = DateTime.UtcNow
            };
        }

        private static ReviewType CreateReviewType(long repositoryId, string name)
        {
            DateTime now = DateTime.UtcNow;

            return new ReviewType
            {
                RepositoryId = repositoryId,
                Name = name,
                FileExtensions = "cs",
                DefaultPriority = 10,
                Enabled = true,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private static QueueItem CreateQueueItem(long repositoryId, long repoFileId, long reviewTypeId, int priority)
        {
            return new QueueItem
            {
                RepositoryId = repositoryId,
                RepoFileId = repoFileId,
                ReviewTypeId = reviewTypeId,
                Priority = priority,
                CreatedDate = DateTime.UtcNow
            };
        }

        private static Finding CreateFinding(long repositoryId, string fingerprint)
        {
            DateTime now = DateTime.UtcNow;

            return new Finding
            {
                RepositoryId = repositoryId,
                Fingerprint = fingerprint,
                Severity = FindingSeverity.Medium,
                Title = "Title",
                Description = "Description",
                Status = FindingStatus.New,
                Category = FindingCategory.CodeIssue,
                CreatedDate = now,
                LastModifiedDate = now
            };
        }

        private sealed class TestDatabase : IDisposable
        {
            private readonly string _directory;

            public TestDatabase()
            {
                _directory = Path.Combine(Path.GetTempPath(), "revathryn-data-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);

                string databasePath = Path.Combine(_directory, "test.db");
                Options = new DatabaseOptions { ConnectionString = $"Data Source={databasePath};Pooling=False" };
                Factory = new SqliteConnectionFactory(Options);

                SqliteDatabaseInitializer initializer = new(Factory);
                initializer.Initialize();
            }

            public DatabaseOptions Options { get; }

            public SqliteConnectionFactory Factory { get; }

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
