using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class EngineTests
    {
        [TestMethod]
        public async Task RepositoryScanner_ReturnsFilesByRelativePath()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src"));
                File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "class Program { }");

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);
                IReadOnlyList<RepoFile> files = scan.DiscoveredFiles;

                Assert.HasCount(1, files);
                Assert.AreEqual("src/Program.cs", files[0].RelativePath);
                Assert.AreEqual("Program.cs", files[0].FileName);
                Assert.AreEqual("cs", files[0].Extension);
                Assert.IsFalse(string.IsNullOrEmpty(files[0].Sha256));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ExcludesBuildFolders()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                Directory.CreateDirectory(Path.Combine(root, "bin"));
                File.WriteAllText(Path.Combine(root, "bin", "artifact.dll"), "binary");

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
        public async Task ReviewQueue_EnqueuesAndDequeuesByPriority()
        {
            FakeQueueRepository repository = new();
            ReviewQueue queue = new(repository);

            await queue.EnqueueAsync(CreateQueueItem(1, 1, 1, 5));
            await queue.EnqueueAsync(CreateQueueItem(1, 2, 1, 50));

            QueueItem dequeued = await queue.DequeueAsync();

            Assert.AreEqual(2, dequeued.RepoFileId);
            Assert.AreEqual(50, dequeued.Priority);
        }

        [TestMethod]
        public async Task ReviewQueue_IgnoresDuplicatePendingItems()
        {
            FakeQueueRepository repository = new();
            ReviewQueue queue = new(repository);

            QueueItem item = CreateQueueItem(1, 1, 1, 5);

            bool first = await queue.EnqueueAsync(item);
            bool second = await queue.EnqueueAsync(item);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
        }

        [TestMethod]
        public async Task ReviewScheduler_EnqueuesOnlyApplicableReviewTypes()
        {
            FakeRepositoryRepository repositories = new();
            FakeRepoFileRepository files = new();
            FakeReviewTypeRepository reviewTypes = new();
            FakeReviewLinkRepository links = new();
            FakeQueueRepository queue = new();

            Repository repository = new() { Id = 1, Name = "Repo", Enabled = true };
            repositories.Add(repository);

            RepoFile csFile = new() { Id = 1, RepositoryId = 1, RelativePath = "src/A.cs", Extension = "cs" };
            RepoFile mdFile = new() { Id = 2, RepositoryId = 1, RelativePath = "docs/A.md", Extension = "md" };
            files.Add(csFile);
            files.Add(mdFile);

            reviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 1, FileExtensions = "cs", DefaultPriority = 20, Enabled = true });
            reviewTypes.Add(new ReviewType { Id = 2, RepositoryId = 1, FileExtensions = "md", DefaultPriority = 10, Enabled = true });

            FakeRepoFileAnalysisRepository analyses = new();
            ReviewScheduler scheduler = new(repositories, files, reviewTypes, links, analyses, new ReviewQueue(queue));

            await scheduler.ScheduleRepositoryAsync(1);

            Assert.HasCount(2, queue.EnqueuedItems);
            Assert.AreEqual(1, queue.EnqueuedItems[0].ReviewTypeId);
            Assert.AreEqual(2, queue.EnqueuedItems[1].ReviewTypeId);
        }

        [TestMethod]
        public async Task RepositoryService_RegisterCopiesDefaultReviewTypes()
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
                    LocalPath = root
                });

                Assert.AreNotEqual(0, registered.Id);
                Assert.HasCount(2, reviewTypes.Added);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-engine-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
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

        private sealed class FakeRepositoryRepository : IRepositoryRepository
        {
            private readonly Dictionary<long, Repository> _items = new();
            private long _nextId = 1;

            public List<Repository> Added { get; } = new List<Repository>();

            public void Add(Repository repository)
            {
                repository.Id = _nextId++;
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
                repository.Id = _nextId++;
                _items[repository.Id] = repository;
                Added.Add(repository);
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
            private readonly Dictionary<long, RepoFile> _items = new();

            public void Add(RepoFile file)
            {
                _items[file.Id] = file;
            }

            public Task<RepoFile> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue(id, out RepoFile file);
                return Task.FromResult(file);
            }

            public Task<IReadOnlyList<RepoFile>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepoFile>>([.. _items.Values.Where(f => f.RepositoryId == repositoryId)]);
            }

            public Task<RepoFile> GetByRelativePathAsync(long repositoryId, string relativePath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_items.Values.FirstOrDefault(f => f.RepositoryId == repositoryId && f.RelativePath == relativePath));
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

                    RepoFile existing = _items.Values.FirstOrDefault(f => f.RepositoryId == repositoryId && f.RelativePath == file.RelativePath);

                    if (existing is not null)
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
                            file.Id = _items.Count + 1;
                        }

                        result.NewFiles++;
                    }

                    _items[file.Id] = file;
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
                _items[file.Id] = file;
                return Task.FromResult(file.Id);
            }

            public Task UpdateAsync(RepoFile file, CancellationToken cancellationToken = default)
            {
                _items[file.Id] = file;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                _items.Remove(id);
                return Task.CompletedTask;
            }

            public Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                foreach (long key in _items.Keys.Where(k => _items[k].RepositoryId == repositoryId).ToList())
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

            public List<ReviewType> Added { get; } = new List<ReviewType>();

            public void Add(ReviewType reviewType)
            {
                reviewType.Id = _nextId++;
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
                reviewType.Id = _nextId++;
                _items[reviewType.Id] = reviewType;
                Added.Add(reviewType);
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

        private sealed class FakeRepoFileAnalysisRepository : IRepoFileAnalysisRepository
        {
            private readonly Dictionary<(long, long), RepoFileAnalysis> _items = new();

            public Task<RepoFileAnalysis> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
            {
                _items.TryGetValue((repoFileId, reviewTypeId), out RepoFileAnalysis analysis);
                return Task.FromResult(analysis);
            }

            public Task<long> UpsertAsync(RepoFileAnalysis analysis, CancellationToken cancellationToken = default)
            {
                analysis.Id = _items.Count + 1;
                _items[(analysis.RepoFileId, analysis.ReviewTypeId)] = analysis;
                return Task.FromResult(analysis.Id);
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                (long, long)? key = _items.Where(pair => pair.Value.Id == id).Select(pair => ((long, long)?)pair.Key).FirstOrDefault();

                if (key is not null)
                {
                    _items.Remove(key.Value);
                }

                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                foreach ((long, long) key in _items.Keys.Where(k => k.Item1 == repoFileId).ToList())
                {
                    _items.Remove(key);
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewLinkRepository : IReviewLinkRepository
        {
            private readonly Dictionary<(long, long), ReviewLink> _items = new();

            public Task<ReviewLink> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_items.Values.FirstOrDefault(l => l.Id == id));
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
                link.Id = _items.Count + 1;
                _items[(link.RepoFileId, link.ReviewTypeId)] = link;
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
                foreach ((long, long) key in _items.Keys.Where(k => k.Item1 == repoFileId).ToList())
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

            public List<QueueItem> EnqueuedItems => [.. _items];

            public Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
            {
                bool duplicate = _items.Any(existing => existing.RepoFileId == item.RepoFileId && existing.ReviewTypeId == item.ReviewTypeId);
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
    }
}
