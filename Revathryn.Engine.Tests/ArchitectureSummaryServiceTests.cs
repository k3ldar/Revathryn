using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ArchitectureSummaryServiceTests
    {
        [TestMethod]
        public async Task GetOrCreateAsync_CachedSummary_ReturnsWithoutCallingEngine()
        {
            FakeArchitectureRepository repository = new()
            {
                Cached = new RepoFileArchitecture { RepoFileId = 1, Sha256 = "abc", Summary = "cached summary" }
            };
            FakeAIEngine engine = new();
            ArchitectureSummaryService service = CreateService(repository, engine);

            string result = await service.GetOrCreateAsync(CreateFile(sha256: "abc"), "content", "CSharp");

            Assert.AreEqual("cached summary", result);
            Assert.AreEqual(0, engine.StructureCallCount);
            Assert.IsEmpty(repository.Upserted);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_MissingCache_GeneratesAndPersists()
        {
            FakeArchitectureRepository repository = new();
            FakeAIEngine engine = new()
            {
                StructureResponse = new ArchitectureAnalysisResponse { Success = true, Summary = "generated summary" }
            };
            ArchitectureSummaryService service = CreateService(repository, engine);

            string result = await service.GetOrCreateAsync(CreateFile(sha256: "abc"), "content", "CSharp");

            Assert.AreEqual("generated summary", result);
            Assert.AreEqual(1, engine.StructureCallCount);
            Assert.HasCount(1, repository.Upserted);
            Assert.AreEqual("abc", repository.Upserted[0].Sha256);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_ChangedSha256_Regenerates()
        {
            FakeArchitectureRepository repository = new()
            {
                Cached = new RepoFileArchitecture { RepoFileId = 1, Sha256 = "old", Summary = "stale summary" }
            };
            FakeAIEngine engine = new()
            {
                StructureResponse = new ArchitectureAnalysisResponse { Success = true, Summary = "fresh summary" }
            };
            ArchitectureSummaryService service = CreateService(repository, engine);

            string result = await service.GetOrCreateAsync(CreateFile(sha256: "new"), "content", "CSharp");

            Assert.AreEqual("fresh summary", result);
            Assert.HasCount(1, repository.Upserted);
            Assert.AreEqual("new", repository.Upserted[0].Sha256);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_FailedAnalysis_ReturnsEmptyAndLogsFailure()
        {
            FakeArchitectureRepository repository = new();
            FakeFailureRepository failures = new();
            FakeAIEngine engine = new()
            {
                StructureResponse = new ArchitectureAnalysisResponse { Success = false, Error = "provider error", RawResponse = "raw" }
            };
            ArchitectureSummaryService service = CreateService(repository, engine, failures);

            string result = await service.GetOrCreateAsync(CreateFile(sha256: "abc"), "content", "CSharp");

            Assert.AreEqual(string.Empty, result);
            Assert.HasCount(1, failures.Added);
            Assert.AreEqual("provider error", failures.Added[0].Error);
            Assert.AreEqual("raw", failures.Added[0].RawResponse);
            Assert.HasCount(0, repository.Upserted);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_SkippedAnalysis_ReturnsEmptyWithoutFailure()
        {
            FakeArchitectureRepository repository = new();
            FakeFailureRepository failures = new();
            FakeAIEngine engine = new()
            {
                StructureResponse = new ArchitectureAnalysisResponse { Success = true, Skipped = true }
            };
            ArchitectureSummaryService service = CreateService(repository, engine, failures);

            string result = await service.GetOrCreateAsync(CreateFile(sha256: "abc"), "content", "CSharp");

            Assert.AreEqual(string.Empty, result);
            Assert.HasCount(0, failures.Added);
            Assert.HasCount(0, repository.Upserted);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_NullFile_ThrowsArgumentNullException()
        {
            ArchitectureSummaryService service = CreateService(new FakeArchitectureRepository(), new FakeAIEngine());

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => service.GetOrCreateAsync(null, "content", "CSharp"));
        }

        private static ArchitectureSummaryService CreateService(
            FakeArchitectureRepository repository,
            FakeAIEngine engine,
            FakeFailureRepository failures = null)
        {
            return new ArchitectureSummaryService(repository, failures ?? new FakeFailureRepository(), engine, new RecordingDiagnostics());
        }

        private static RepoFile CreateFile(string sha256)
        {
            return new RepoFile
            {
                Id = 1,
                RepositoryId = 1,
                RelativePath = "Program.cs",
                Sha256 = sha256
            };
        }

        private sealed class FakeArchitectureRepository : IRepoFileArchitectureRepository
        {
            public RepoFileArchitecture Cached { get; set; }

            public List<RepoFileArchitecture> Upserted { get; } = [];

            public Task<RepoFileArchitecture> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Cached);
            }

            public Task<long> UpsertAsync(RepoFileArchitecture architecture, CancellationToken cancellationToken = default)
            {
                Upserted.Add(architecture);
                return Task.FromResult((long)Upserted.Count);
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeFailureRepository : IArchitectureAnalysisFailureRepository
        {
            public List<ArchitectureAnalysisFailure> Added { get; } = [];

            public Task<long> AddAsync(ArchitectureAnalysisFailure failure, CancellationToken cancellationToken = default)
            {
                Added.Add(failure);
                return Task.FromResult((long)Added.Count);
            }

            public Task<IReadOnlyList<ArchitectureAnalysisFailure>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ArchitectureAnalysisFailure>>(Added);
            }

            public Task<ArchitectureAnalysisFailure> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ArchitectureAnalysisFailure>(null);
            }
        }

        private sealed class FakeAIEngine : IArchitectureAnalysisEngine
        {
            public ArchitectureAnalysisResponse StructureResponse { get; set; } = new();

            public int StructureCallCount { get; private set; }

            public Task<ArchitectureAnalysisResponse> AnalyseStructureAsync(ArchitectureAnalysisRequest request, CancellationToken cancellationToken = default)
            {
                StructureCallCount++;
                return Task.FromResult(StructureResponse);
            }
        }
    }
}
