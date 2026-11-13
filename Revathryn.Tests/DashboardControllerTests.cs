using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Controllers;
using Revathryn.Models;
using Revathryn.Models.Dashboard;
using Revathryn.Resources;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DashboardControllerTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Index_ReturnsViewWithSnapshot()
        {
            DashboardSnapshot snapshot = new()
            {
                ConfiguredRepositoryCount = 4
            };

            FakeDashboardQueryService dashboardQuery = new()
            {
                Snapshot = snapshot
            };

            DashboardController controller = CreateController(dashboardQuery: dashboardQuery);

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(snapshot, viewResult.Model);
        }

        [TestMethod]
        public async Task Data_ReturnsPartialViewWithSnapshot()
        {
            DashboardSnapshot snapshot = new()
            {
                QueuedAnalysisCount = 7
            };

            FakeDashboardQueryService dashboardQuery = new()
            {
                Snapshot = snapshot
            };

            DashboardController controller = CreateController(dashboardQuery: dashboardQuery);

            IActionResult result = await controller.Data(TestContext.CancellationToken);

            PartialViewResult partialView = result as PartialViewResult;
            Assert.IsNotNull(partialView);
            Assert.AreEqual(Consts.DashboardContentViewName, partialView.ViewName);
            Assert.AreSame(snapshot, partialView.Model);
        }

        [TestMethod]
        public void Error_ReturnsViewWithRequestTraceIdentifier()
        {
            ControllerContext context = ControllerTestSupport.CreateControllerContext(out DefaultHttpContext httpContext);
            httpContext.TraceIdentifier = "trace-123";
            Activity.Current = null;

            DashboardController controller = CreateController();
            controller.ControllerContext = context;

            IActionResult result = controller.Error();

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            ErrorViewModel model = viewResult.Model as ErrorViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("trace-123", model.RequestId);
        }

        [TestMethod]
        public async Task Repository_ReturnsNotFoundWhenRepositoryMissing()
        {
            DashboardController controller = CreateController();

            IActionResult result = await controller.Repository(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Repository_BuildsDetailsViewModelWithModulesAndFindings()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Program.cs", ProjectPath = "src/app.csproj" };
            RepoFile unassigned = new() { Id = 20, RepositoryId = 1, RelativePath = "README.md" };

            ReviewType reviewType = new() { Id = 100, Name = "CSharp" };

            Finding finding = new()
            {
                Id = 1,
                RepositoryId = 1,
                RepoFileId = 10,
                ReviewTypeId = 100,
                Severity = FindingSeverity.High,
                Category = FindingCategory.CodeIssue,
                Status = FindingStatus.New
            };

            Finding operationalFailure = new()
            {
                Id = 2,
                RepositoryId = 1,
                RepoFileId = 10,
                ReviewTypeId = 100,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.OperationalFailure,
                Status = FindingStatus.New
            };

            RepositoryProject project = new()
            {
                Id = 1,
                RepositoryId = 1,
                Kind = ProjectKind.Project,
                RelativePath = "src/app.csproj",
                Name = "App"
            };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);
            files.Files.Add(unassigned);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);
            findings.Findings.Add(operationalFailure);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeProjectRepository projects = new();
            projects.Projects.Add(project);

            FakeReviewScheduler scheduler = new()
            {
                ApplicableResolver = repoFile => repoFile.Id == 10 ? [reviewType] : [],
                CurrentResolver = (repoFile, currentReviewType) => true
            };

            DashboardController controller = CreateController(
                repositories: repositories,
                files: files,
                findings: findings,
                reviewTypes: reviewTypes,
                projects: projects,
                scheduler: scheduler);

            IActionResult result = await controller.Repository(1, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryDetailsViewModel model = viewResult.Model as RepositoryDetailsViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(repository, model.Repository);
            Assert.AreEqual(2, model.FilesDiscovered);
            Assert.AreEqual(1, model.FilesRequiringReview);
            Assert.AreEqual(0, model.OutstandingReviews);
            Assert.AreEqual(1, model.FindingsCount);
            Assert.AreEqual(1, model.OperationalFailuresCount);
            Assert.AreEqual(Strings.RepositoryReviewStatusIssuesFound, model.ReviewStatus);

            Assert.HasCount(1, model.Modules);
            ModuleViewModel module = model.Modules[0];
            Assert.AreEqual("App", module.Name);
            Assert.AreEqual("src/app.csproj", module.RelativePath);
            Assert.HasCount(1, module.Files);
            Assert.AreEqual(1, module.RequiredReviews);
            Assert.AreEqual(1, module.CompletedReviews);
            Assert.AreEqual(0, module.OutstandingReviews);
            Assert.AreEqual(100, module.ReviewCoveragePercent);

            Assert.HasCount(1, module.FindingGroups);
            FindingGroupViewModel group = module.FindingGroups[0];
            Assert.AreEqual(FindingSeverity.High, group.Severity);
            Assert.AreEqual("CSharp", group.ReviewTypeName);
            Assert.HasCount(1, group.Findings);

            Assert.HasCount(1, model.UnassignedFiles);
            Assert.AreEqual(20, model.UnassignedFiles[0].File.Id);
            Assert.AreEqual(0, model.UnassignedFiles[0].FindingsCount);
        }

        [TestMethod]
        public async Task Repository_BuildsNotScannedStateWhenNoFiles()
        {
            Repository repository = new() { Id = 2, Name = "Empty" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            DashboardController controller = CreateController(repositories: repositories);

            IActionResult result = await controller.Repository(2, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryDetailsViewModel model = viewResult.Model as RepositoryDetailsViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.FilesDiscovered);
            Assert.AreEqual(0, model.FilesRequiringReview);
            Assert.AreEqual(0, model.OutstandingReviews);
            Assert.AreEqual(0, model.FindingsCount);
            Assert.AreEqual(Strings.RepositoryReviewStatusNotScanned, model.ReviewStatus);
            Assert.HasCount(0, model.Modules);
            Assert.HasCount(0, model.UnassignedFiles);
        }

        [TestMethod]
        public async Task File_ReturnsNotFoundWhenFileMissing()
        {
            DashboardController controller = CreateController();

            IActionResult result = await controller.File(50, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task File_ReturnsNotFoundWhenRepositoryMissing()
        {
            RepoFile file = new() { Id = 60, RepositoryId = 3 };

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            DashboardController controller = CreateController(files: files);

            IActionResult result = await controller.File(60, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task File_BuildsDetailsViewModelWithCompletedReview()
        {
            Repository repository = new() { Id = 3, Name = "Repo" };
            RepoFile file = new() { Id = 60, RepositoryId = 3, RelativePath = "src/Program.cs" };

            ReviewType reviewType = new() { Id = 200, Name = "Security" };

            RepoFileAnalysis analysis = new()
            {
                Id = 5,
                RepositoryId = 3,
                RepoFileId = 60,
                ReviewTypeId = 200
            };

            Finding finding = new()
            {
                Id = 9,
                RepositoryId = 3,
                RepoFileId = 60,
                ReviewTypeId = 200,
                Severity = FindingSeverity.Medium,
                Category = FindingCategory.CodeIssue
            };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeRepoFileAnalysisRepository analyses = new();
            analyses.Analyses.Add(analysis);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewScheduler scheduler = new()
            {
                ApplicableResolver = repoFile => [reviewType],
                CurrentResolver = (repoFile, currentReviewType) => true
            };

            DashboardController controller = CreateController(
                repositories: repositories,
                files: files,
                reviewTypes: reviewTypes,
                analyses: analyses,
                findings: findings,
                scheduler: scheduler);

            IActionResult result = await controller.File(60, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            FileDetailsViewModel model = viewResult.Model as FileDetailsViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(repository, model.Repository);
            Assert.AreSame(file, model.File);

            Assert.HasCount(1, model.ApplicableReviewTypes);
            Assert.HasCount(1, model.CompletedReviews);
            Assert.AreEqual("Security", model.CompletedReviews[0].ReviewTypeName);
            Assert.AreSame(analysis, model.CompletedReviews[0].Analysis);
            Assert.HasCount(0, model.OutstandingReviewTypes);

            Assert.HasCount(1, model.Findings);
            Assert.AreEqual("Security", model.Findings[0].ReviewTypeName);
            Assert.AreEqual("src/Program.cs", model.Findings[0].FileRelativePath);
        }

        [TestMethod]
        public async Task File_PlacesReviewTypeInOutstandingWhenNotCurrent()
        {
            Repository repository = new() { Id = 4, Name = "Repo" };
            RepoFile file = new() { Id = 70, RepositoryId = 4, RelativePath = "src/Program.cs" };

            ReviewType reviewType = new() { Id = 300, Name = "Style" };

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

            DashboardController controller = CreateController(
                repositories: repositories,
                files: files,
                reviewTypes: reviewTypes,
                scheduler: scheduler);

            IActionResult result = await controller.File(70, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            FileDetailsViewModel model = viewResult.Model as FileDetailsViewModel;
            Assert.IsNotNull(model);

            Assert.HasCount(0, model.CompletedReviews);
            Assert.HasCount(1, model.OutstandingReviewTypes);
            Assert.AreSame(reviewType, model.OutstandingReviewTypes[0]);
            Assert.HasCount(0, model.Findings);
        }

        private static DashboardController CreateController(
            FakeDashboardQueryService dashboardQuery = null,
            FakeRepositoryService repositories = null,
            FakeRepositoryFileService files = null,
            FakeFindingRepository findings = null,
            FakeRepoFileAnalysisRepository analyses = null,
            FakeReviewTypeRepository reviewTypes = null,
            FakeProjectRepository projects = null,
            FakeReviewScheduler scheduler = null)
        {
            FakeDashboardQueryService dashboardService = dashboardQuery ?? new();
            FakeRepositoryService repositoryService = repositories ?? new();
            FakeRepositoryFileService fileService = files ?? new();
            FakeFindingRepository findingRepository = findings ?? new();
            FakeRepoFileAnalysisRepository analysisRepository = analyses ?? new();
            FakeReviewTypeRepository reviewTypeRepository = reviewTypes ?? new();
            FakeProjectRepository projectRepository = projects ?? new();
            FakeReviewScheduler reviewScheduler = scheduler ?? new();

            ControllerContext context = ControllerTestSupport.CreateControllerContext(out _);

            return new DashboardController(
                dashboardService,
                repositoryService,
                fileService,
                findingRepository,
                analysisRepository,
                reviewTypeRepository,
                projectRepository,
                reviewScheduler)
            {
                ControllerContext = context
            };
        }

        private sealed class FakeDashboardQueryService : IDashboardQueryService
        {
            public DashboardSnapshot Snapshot { get; set; } = new();

            public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Snapshot);
            }
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
                IReadOnlyList<RepositoryIssueCount> result = [
                    .. Findings
                        .GroupBy(finding => finding.RepositoryId)
                        .Select(group => new RepositoryIssueCount { RepositoryId = group.Key, Count = group.Count() })
                ];
                return Task.FromResult(result);
            }

            public Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Finding>>([]);
            }

            public Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Findings.Count(finding => finding.Category == FindingCategory.OperationalFailure));
            }

            public Task<IReadOnlyList<Finding>> GetSimilarityCandidatesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task MarkDuplicateAsync(long findingId, long duplicateOfFindingId, double similarityScore, DateTime lastModifiedUtc, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
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

        private sealed class FakeProjectRepository : IProjectRepository
        {
            public List<RepositoryProject> Projects { get; } = [];

            public Task<IReadOnlyList<RepositoryProject>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepositoryProject> result = [.. Projects.Where(project => project.RepositoryId == repositoryId)];
                return Task.FromResult(result);
            }

            public Task ReconcileAsync(long repositoryId, IReadOnlyList<RepositoryProject> projects, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewScheduler : IReviewScheduler
        {
            public Func<RepoFile, IReadOnlyList<ReviewType>> ApplicableResolver { get; set; } = repoFile => [];

            public Func<RepoFile, ReviewType, bool> CurrentResolver { get; set; } = (repoFile, reviewType) => false;

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
                return Task.FromResult(new ReviewScheduleResult());
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
    }
}
