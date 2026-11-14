using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Controllers;
using Revathryn.Models.Api;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class FileDetailsControllerTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Get_MissingRepository_ReturnsBadRequest()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Get(null, "src/Program.cs", TestContext.CancellationToken);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_WhitespaceRepository_ReturnsBadRequest()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Get("   ", "src/Program.cs", TestContext.CancellationToken);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_MissingFile_ReturnsBadRequest()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Get("Repo", null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_WhitespaceFile_ReturnsBadRequest()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Get("Repo", "   ", TestContext.CancellationToken);

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_UnknownRepository_ReturnsNotFound()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Get("Missing", "src/Program.cs", TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_UnknownFile_ReturnsNotFound()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FileDetailsController controller = CreateController(repositories: repositories);

            IActionResult result = await controller.Get("Repo", "src/Missing.cs", TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task Get_ReturnsAggregatedFileDetails()
        {
            DateTime analysedUtc = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            DateTime contextUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Repository repository = new() { Id = 1, Name = "Demo", LocalPath = @"C:\repo" };
            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Program.cs", FileName = "Program.cs" };

            RepoFileArchitecture architecture = new()
            {
                Id = 1,
                RepositoryId = 1,
                RepoFileId = 10,
                Summary = "Entry point"
            };

            ReviewType reviewType = new() { Id = 100, Name = "Security" };

            RepoFileAnalysis analysis = new()
            {
                Id = 5,
                RepositoryId = 1,
                RepoFileId = 10,
                ReviewTypeId = 100,
                LastAnalysedDate = analysedUtc,
                AnalysisVersion = "1.0",
                ReviewDefinitionVersion = "2.0",
                Branch = "main"
            };

            Finding finding = new()
            {
                Id = 1,
                RepositoryId = 1,
                RepoFileId = 10,
                ReviewTypeId = 100,
                ReviewContextId = 7,
                Severity = FindingSeverity.High,
                Category = FindingCategory.CodeIssue,
                Status = FindingStatus.New,
                Title = "Insecure input"
            };

            AIReviewContext context = new()
            {
                Id = 7,
                EngineName = "Revathryn",
                EngineProvider = AIProvider.OpenAI.ToString(),
                EngineModel = "gpt-4",
                Language = "C#",
                ReviewDefinitionVersion = "2.0",
                RequestedContextKeys = ["file"],
                Branch = "main",
                CreatedUtc = contextUtc
            };

            IssueValidation validation = new()
            {
                Id = 50,
                FindingId = 1,
                Status = IssueValidationStatus.Completed,
                Verdict = IssueValidationVerdict.Agreed,
                Confidence = 0.95,
                Provider = AIProvider.OpenAI,
                Model = "gpt-4",
                CreatedUtc = contextUtc,
                CompletedUtc = contextUtc.AddMinutes(1),
                OriginalSeverity = FindingSeverity.High,
                OriginalTitle = "Insecure input"
            };

            IndexedSymbol symbol = new()
            {
                Name = "Program",
                Kind = "Class",
                FullyQualifiedName = "Demo.Program",
                UsingDirectives = ["System", "System.Text", "Demo.Orders"],
                Dependencies = ["Demo.Customer", "System.Linq", "Microsoft.Extensions.Logging"]
            };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeReviewContextRepository contexts = new();
            contexts.Contexts.Add(context);

            FakeIssueValidationService validations = new();
            validations.Validations.Add(validation);

            FakeRepoFileArchitectureRepository architectures = new();
            architectures.Architectures.Add(architecture);

            FakeRepoFileAnalysisRepository analyses = new();
            analyses.Analyses.Add(analysis);

            FakeReviewScheduler scheduler = new()
            {
                ApplicableResolver = repoFile => [reviewType],
                CurrentResolver = (repoFile, currentReviewType) => true
            };

            FakeSymbolIndexStore symbols = new();
            symbols.Symbols.Add(symbol);

            FileDetailsController controller = CreateController(
                repositories: repositories,
                files: files,
                findings: findings,
                reviewTypes: reviewTypes,
                contexts: contexts,
                validations: validations,
                architectures: architectures,
                analyses: analyses,
                scheduler: scheduler,
                symbols: symbols);

            IActionResult result = await controller.Get("demo", @"src\Program.cs", TestContext.CancellationToken);

            OkObjectResult ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            FileDetailsResponse response = ok.Value as FileDetailsResponse;
            Assert.IsNotNull(response);
            Assert.AreSame(repository, response.Repository);
            Assert.AreSame(file, response.File);
            Assert.AreSame(architecture, response.Architecture);
            Assert.IsGreaterThan(DateTime.MinValue, response.ExportedUtc);

            Assert.HasCount(1, response.Reviews);
            FileReviewStatus reviewStatus = response.Reviews[0];
            Assert.AreEqual(100L, reviewStatus.ReviewTypeId);
            Assert.AreEqual("Security", reviewStatus.ReviewTypeName);
            Assert.AreEqual(Consts.ReviewStatusCompleted, reviewStatus.Status);
            Assert.AreEqual(analysedUtc, reviewStatus.LastAnalysedUtc);
            Assert.AreEqual("1.0", reviewStatus.AnalysisVersion);
            Assert.AreEqual("2.0", reviewStatus.ReviewDefinitionVersion);
            Assert.AreEqual("main", reviewStatus.Branch);

            Assert.HasCount(1, response.Issues);
            FileIssueDetails issue = response.Issues[0];
            Assert.AreSame(finding, issue.Finding);
            Assert.AreEqual("Security", issue.ReviewTypeName);
            Assert.AreEqual("src/Program.cs", issue.FileRelativePath);

            Assert.IsNotNull(issue.Context);
            Assert.AreEqual(7L, issue.Context.Id);
            Assert.AreEqual("Revathryn", issue.Context.EngineName);
            Assert.AreEqual("OpenAI", issue.Context.EngineProvider);
            Assert.AreEqual("gpt-4", issue.Context.EngineModel);
            Assert.AreEqual("C#", issue.Context.Language);
            Assert.AreEqual("2.0", issue.Context.ReviewDefinitionVersion);
            Assert.HasCount(1, issue.Context.RequestedContextKeys);
            Assert.AreEqual("main", issue.Context.Branch);
            Assert.AreEqual(contextUtc, issue.Context.CreatedUtc);

            Assert.IsNotNull(issue.LatestValidation);
            Assert.AreEqual("Completed", issue.LatestValidation.Status);
            Assert.AreEqual("Agreed", issue.LatestValidation.Verdict);
            Assert.AreEqual(0.95, issue.LatestValidation.Confidence.Value);
            Assert.IsTrue(issue.LatestValidation.IsLatest);

            Assert.HasCount(1, response.Symbols);
            Assert.AreSame(symbol, response.Symbols[0]);
            Assert.HasCount(4, response.SystemDependencies);
            Assert.AreEqual("Microsoft.Extensions.Logging", response.SystemDependencies[0]);
            Assert.AreEqual("System", response.SystemDependencies[1]);
            Assert.AreEqual("System.Linq", response.SystemDependencies[2]);
            Assert.AreEqual("System.Text", response.SystemDependencies[3]);
            Assert.HasCount(2, response.OtherDependencies);
            Assert.AreEqual("Demo.Customer", response.OtherDependencies[0]);
            Assert.AreEqual("Demo.Orders", response.OtherDependencies[1]);
            Assert.AreEqual("Demo", symbols.LastRequestedRepositoryName);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(@"C:\repo", "src\\Program.cs")), symbols.LastRequestedSourcePath);
            Assert.IsTrue(Path.IsPathRooted(symbols.LastRequestedSourcePath));
        }

        [TestMethod]
        public async Task Get_ReturnsEmptyCollectionsWhenNoData()
        {
            Repository repository = new() { Id = 2, Name = "Empty", LocalPath = @"C:\empty" };
            RepoFile file = new() { Id = 20, RepositoryId = 2, RelativePath = "src/Program.cs", FileName = "Program.cs" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FileDetailsController controller = CreateController(repositories: repositories, files: files);

            IActionResult result = await controller.Get("Empty", "src/Program.cs", TestContext.CancellationToken);

            OkObjectResult ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            FileDetailsResponse response = ok.Value as FileDetailsResponse;
            Assert.IsNotNull(response);
            Assert.AreSame(repository, response.Repository);
            Assert.AreSame(file, response.File);
            Assert.IsNull(response.Architecture);
            Assert.HasCount(0, response.Reviews);
            Assert.HasCount(0, response.Issues);
            Assert.HasCount(0, response.Symbols);
            Assert.HasCount(0, response.SystemDependencies);
            Assert.HasCount(0, response.OtherDependencies);
        }

        [TestMethod]
        public async Task Get_PlacesReviewTypeInOutstandingWhenNotCurrent()
        {
            Repository repository = new() { Id = 3, Name = "Repo", LocalPath = @"C:\repo" };
            RepoFile file = new() { Id = 30, RepositoryId = 3, RelativePath = "src/Program.cs", FileName = "Program.cs" };

            ReviewType reviewType = new() { Id = 200, Name = "Style" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeReviewScheduler scheduler = new()
            {
                ApplicableResolver = repoFile => [reviewType],
                CurrentResolver = (repoFile, currentReviewType) => false
            };

            FileDetailsController controller = CreateController(
                repositories: repositories,
                files: files,
                reviewTypes: reviewTypes,
                scheduler: scheduler);

            IActionResult result = await controller.Get("Repo", "src/Program.cs", TestContext.CancellationToken);

            OkObjectResult ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            FileDetailsResponse response = ok.Value as FileDetailsResponse;
            Assert.IsNotNull(response);

            Assert.HasCount(1, response.Reviews);
            FileReviewStatus reviewStatus = response.Reviews[0];
            Assert.AreEqual(200L, reviewStatus.ReviewTypeId);
            Assert.AreEqual("Style", reviewStatus.ReviewTypeName);
            Assert.AreEqual(Consts.ReviewStatusOutstanding, reviewStatus.Status);
            Assert.IsNull(reviewStatus.LastAnalysedUtc);
            Assert.AreEqual(string.Empty, reviewStatus.AnalysisVersion);
            Assert.AreEqual(string.Empty, reviewStatus.ReviewDefinitionVersion);
            Assert.AreEqual(string.Empty, reviewStatus.Branch);
        }

        [TestMethod]
        public async Task Get_MatchesFileByFileNameCaseInsensitively()
        {
            Repository repository = new() { Id = 4, Name = "Repo", LocalPath = @"C:\repo" };
            RepoFile file = new() { Id = 40, RepositoryId = 4, RelativePath = "src/Program.cs", FileName = "Program.cs" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FileDetailsController controller = CreateController(repositories: repositories, files: files);

            IActionResult result = await controller.Get("Repo", "program.cs", TestContext.CancellationToken);

            OkObjectResult ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            FileDetailsResponse response = ok.Value as FileDetailsResponse;
            Assert.IsNotNull(response);
            Assert.AreSame(file, response.File);
        }

        [TestMethod]
        public async Task Get_NormalizesBackslashesAndMatchesCaseInsensitively()
        {
            Repository repository = new() { Id = 5, Name = "Repo", LocalPath = @"C:\repo" };
            RepoFile file = new() { Id = 50, RepositoryId = 5, RelativePath = "src/Program.cs", FileName = "Program.cs" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FileDetailsController controller = CreateController(repositories: repositories, files: files);

            IActionResult result = await controller.Get("REPO", @"SRC\PROGRAM.CS", TestContext.CancellationToken);

            OkObjectResult ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            FileDetailsResponse response = ok.Value as FileDetailsResponse;
            Assert.IsNotNull(response);
            Assert.AreSame(file, response.File);
        }

        [TestMethod]
        public async Task Get_IgnoresRemovedFiles()
        {
            Repository repository = new() { Id = 6, Name = "Repo", LocalPath = @"C:\repo" };
            RepoFile removed = new() { Id = 60, RepositoryId = 6, RelativePath = "src/Old.cs", FileName = "Old.cs", IsRemoved = true };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(removed);

            FileDetailsController controller = CreateController(repositories: repositories, files: files);

            IActionResult result = await controller.Get("Repo", "src/Old.cs", TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundObjectResult>(result);
        }

        [TestMethod]
        public async Task UpdateSeverity_UpdatesFindingAndReturnsOk()
        {
            Finding finding = new() { Id = 1, Severity = FindingSeverity.Info };
            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FileDetailsController controller = CreateController(findings: findings);

            IActionResult result = await controller.UpdateSeverity(
                1,
                new UpdateFindingSeverityRequest { Severity = FindingSeverity.Critical },
                TestContext.CancellationToken);

            Assert.IsInstanceOfType<OkObjectResult>(result);
            Assert.AreEqual(FindingSeverity.Critical, finding.Severity);
        }

        [TestMethod]
        public async Task UpdateSeverity_UnknownFinding_ReturnsNotFound()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.UpdateSeverity(
                99,
                new UpdateFindingSeverityRequest { Severity = FindingSeverity.Low },
                TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UpdateStatus_UpdatesFindingAndReturnsOk()
        {
            Finding finding = new() { Id = 1, Status = FindingStatus.New };
            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FileDetailsController controller = CreateController(findings: findings);

            IActionResult result = await controller.UpdateStatus(
                1,
                new UpdateFindingStatusRequest { Status = FindingStatus.Resolved },
                TestContext.CancellationToken);

            Assert.IsInstanceOfType<OkObjectResult>(result);
            Assert.AreEqual(FindingStatus.Resolved, finding.Status);
        }

        [TestMethod]
        public async Task UpdateStatus_UnknownFinding_ReturnsNotFound()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.UpdateStatus(
                99,
                new UpdateFindingStatusRequest { Status = FindingStatus.Reviewed },
                TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Resubmit_QueuesFileAndReturnsResult()
        {
            Finding finding = new() { Id = 1, RepoFileId = 10, ReviewTypeId = 100 };
            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewScheduler scheduler = new()
            {
                RequeueResult = new ReviewScheduleResult { Added = 1, DuplicatesSkipped = 0 }
            };

            FileDetailsController controller = CreateController(findings: findings, scheduler: scheduler);

            IActionResult result = await controller.Resubmit(1, TestContext.CancellationToken);

            Assert.IsInstanceOfType<OkObjectResult>(result);
            Assert.AreEqual(10L, scheduler.LastRequeuedRepoFileId);
            Assert.AreEqual(100L, scheduler.LastRequeuedReviewTypeId);
        }

        [TestMethod]
        public async Task Resubmit_MissingFileOrReviewType_ReturnsUnprocessableEntity()
        {
            Finding finding = new() { Id = 1 };
            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FileDetailsController controller = CreateController(findings: findings);

            IActionResult result = await controller.Resubmit(1, TestContext.CancellationToken);

            Assert.IsInstanceOfType<UnprocessableEntityObjectResult>(result);
        }

        [TestMethod]
        public async Task Resubmit_UnknownFinding_ReturnsNotFound()
        {
            FileDetailsController controller = CreateController();

            IActionResult result = await controller.Resubmit(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        private static FileDetailsController CreateController(
            FakeRepositoryService repositories = null,
            FakeRepositoryFileService files = null,
            FakeFindingRepository findings = null,
            FakeReviewTypeRepository reviewTypes = null,
            FakeReviewContextRepository contexts = null,
            FakeIssueValidationService validations = null,
            FakeRepoFileArchitectureRepository architectures = null,
            FakeRepoFileAnalysisRepository analyses = null,
            FakeReviewScheduler scheduler = null,
            FakeSymbolIndexStore symbols = null)
        {
            FakeRepositoryService repositoryService = repositories ?? new();
            FakeRepositoryFileService fileService = files ?? new();
            FakeFindingRepository findingRepository = findings ?? new();
            FakeReviewTypeRepository reviewTypeRepository = reviewTypes ?? new();
            FakeReviewContextRepository reviewContextRepository = contexts ?? new();
            FakeIssueValidationService validationService = validations ?? new();
            FakeRepoFileArchitectureRepository architectureRepository = architectures ?? new();
            FakeRepoFileAnalysisRepository analysisRepository = analyses ?? new();
            FakeReviewScheduler reviewScheduler = scheduler ?? new();
            FakeSymbolIndexStore symbolIndexStore = symbols ?? new();

            ControllerContext context = ControllerTestSupport.CreateControllerContext(out _);

            return new FileDetailsController(
                repositoryService,
                fileService,
                findingRepository,
                reviewTypeRepository,
                reviewContextRepository,
                validationService,
                architectureRepository,
                analysisRepository,
                reviewScheduler,
                symbolIndexStore)
            {
                ControllerContext = context
            };
        }

        private sealed class FakeRepositoryService : IRepositoryService
        {
            public List<Repository> Repositories { get; } = [];

            public Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Repository>(null);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<Repository> GetAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                Repository match = Repositories.FirstOrDefault(repository => repository.Id == repositoryId);
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>(Repositories);
            }

            public Task SetEnabledAsync(long repositoryId, bool enabled, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepositoryFileService : IRepositoryFileService
        {
            public List<RepoFile> Files { get; } = [];

            public Task<IReadOnlyList<RepoFile>> GetFilesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepoFile> result = [.. Files.Where(file => file.RepositoryId == repositoryId)];
                return Task.FromResult(result);
            }

            public Task<RepoFile> GetFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                RepoFile match = Files.FirstOrDefault(file => file.Id == repoFileId);
                return Task.FromResult(match);
            }

            public Task SetExcludedAsync(long repoFileId, bool isExcluded, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<RepoFile>> RescanAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepoFile>>([]);
            }
        }

        private sealed class FakeFindingRepository : IFindingRepository
        {
            public List<Finding> Findings { get; } = [];

            public Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                Finding match = Findings.FirstOrDefault(finding => finding.Id == id);
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<Finding> result = [.. Findings.Where(finding => finding.RepositoryId == repositoryId)];
                return Task.FromResult(result);
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
                return Task.FromResult(0);
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
                return Task.FromResult((long)Findings.Count + 1);
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

        private sealed class FakeReviewTypeRepository : IReviewTypeRepository
        {
            public List<ReviewType> ReviewTypes { get; } = [];

            public Task<ReviewType> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                ReviewType match = ReviewTypes.FirstOrDefault(reviewType => reviewType.Id == id);
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<ReviewType>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. ReviewTypes]);
            }

            public Task<IReadOnlyList<ReviewType>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. ReviewTypes]);
            }

            public Task<IReadOnlyList<ReviewType>> GetEnabledByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. ReviewTypes]);
            }

            public Task<IReadOnlyList<RepositoryReviewType>> GetRepositoryReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepositoryReviewType> result = [.. ReviewTypes.Select(reviewType => new RepositoryReviewType { ReviewType = reviewType, Enabled = true })];
                return Task.FromResult(result);
            }

            public Task<long> AddAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                return Task.FromResult((long)ReviewTypes.Count + 1);
            }

            public Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewContextRepository : IReviewContextRepository
        {
            public List<AIReviewContext> Contexts { get; } = [];

            public Task<AIReviewContext> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                AIReviewContext match = Contexts.FirstOrDefault(context => context.Id == id);
                return Task.FromResult(match);
            }

            public Task<long> AddAsync(AIReviewContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult((long)Contexts.Count + 1);
            }
        }

        private sealed class FakeRepoFileArchitectureRepository : IRepoFileArchitectureRepository
        {
            public List<RepoFileArchitecture> Architectures { get; } = [];

            public Task<RepoFileArchitecture> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                RepoFileArchitecture match = Architectures.FirstOrDefault(architecture => architecture.RepoFileId == repoFileId);
                return Task.FromResult(match);
            }

            public Task<long> UpsertAsync(RepoFileArchitecture architecture, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
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

        private sealed class FakeRepoFileAnalysisRepository : IRepoFileAnalysisRepository
        {
            public List<RepoFileAnalysis> Analyses { get; } = [];

            public Task<RepoFileAnalysis> GetAsync(long repoFileId, long reviewTypeId, string branch, CancellationToken cancellationToken = default)
            {
                RepoFileAnalysis match = Analyses.FirstOrDefault(analysis => analysis.RepoFileId == repoFileId && analysis.ReviewTypeId == reviewTypeId && analysis.Branch == (branch ?? string.Empty));
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<RepoFileAnalysis>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepoFileAnalysis> result = [.. Analyses.Where(analysis => analysis.RepositoryId == repositoryId)];
                return Task.FromResult(result);
            }

            public Task<long> UpsertAsync(RepoFileAnalysis analysis, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewScheduler : IReviewScheduler
        {
            public Func<RepoFile, IReadOnlyList<ReviewType>> ApplicableResolver { get; set; } = repoFile => [];

            public Func<RepoFile, ReviewType, bool> CurrentResolver { get; set; } = (repoFile, reviewType) => false;

            public ReviewScheduleResult RequeueResult { get; set; } = new();

            public long LastRequeuedRepoFileId { get; private set; }

            public long LastRequeuedReviewTypeId { get; private set; }

            public Task<ReviewScheduleResult> ScheduleRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReviewScheduleResult());
            }

            public Task<ReviewScheduleResult> ScheduleFileAsync(long repoFileId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReviewScheduleResult());
            }

            public Task<ReviewScheduleResult> RequeueFileAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
            {
                LastRequeuedRepoFileId = repoFileId;
                LastRequeuedReviewTypeId = reviewTypeId;
                return Task.FromResult(RequeueResult);
            }

            public Task<IReadOnlyList<ReviewType>> GetApplicableReviewTypesAsync(RepoFile file, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(ApplicableResolver(file));
            }

            public Task<bool> IsCurrentAsync(RepoFile file, ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CurrentResolver(file, reviewType));
            }
        }

        private sealed class FakeSymbolIndexStore : ISymbolIndexStore
        {
            public List<IndexedSymbol> Symbols { get; } = [];

            public string LastRequestedRepositoryName { get; private set; } = string.Empty;

            public string LastRequestedSourcePath { get; private set; } = string.Empty;

            public Task UpsertFileAsync(string repositoryName, IndexedFile file, IReadOnlyList<IndexedSymbol> symbols, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsForFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
            {
                LastRequestedRepositoryName = repositoryName;
                LastRequestedSourcePath = sourcePath;
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([.. Symbols]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsByNamesAsync(string repositoryName, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsReferencingAsync(string repositoryName, IReadOnlyCollection<string> targetNames, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> SearchSymbolsAsync(string repositoryName, string searchText, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }
        }
    }
}
