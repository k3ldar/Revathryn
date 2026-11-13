using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Controllers;
using Revathryn.Engine;
using Revathryn.Models.Issues;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssuesControllerTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Index_ReturnsRepositoriesWithIssueCountsOrderedByName()
        {
            Repository beta = new() { Id = 1, Name = "Beta", LocalPath = @"C:\src\beta" };
            Repository alpha = new() { Id = 2, Name = "Alpha", LocalPath = @"C:\src\alpha" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.AddRange([beta, alpha]);

            FakeFindingRepository findings = new();
            findings.Findings.AddRange(
            [
                new Finding { Id = 1, RepositoryId = 1, Fingerprint = "fp-1" },
                new Finding { Id = 2, RepositoryId = 1, Fingerprint = "fp-2" },
                new Finding { Id = 3, RepositoryId = 2, Fingerprint = "fp-3" }
            ]);

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueRepositoryListViewModel model = viewResult.Model as IssueRepositoryListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(2, model.Repositories);
            Assert.AreEqual("Alpha", model.Repositories[0].Repository.Name);
            Assert.AreEqual(1, model.Repositories[0].IssueCount);
            Assert.AreEqual("Beta", model.Repositories[1].Repository.Name);
            Assert.AreEqual(2, model.Repositories[1].IssueCount);
        }

        [TestMethod]
        public async Task Index_ReturnsEmptyListWhenNoRepositories()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueRepositoryListViewModel model = viewResult.Model as IssueRepositoryListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(0, model.Repositories);
        }

        [TestMethod]
        public async Task List_ReturnsNotFoundWhenRepositoryMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.List(99, null, null, null, null, null, null, null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task List_AppliesDefaultPageSizeAndPriorityOrdering()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new()
            {
                QueryResult = new FindingQueryResult
                {
                    Items = [new Finding { Id = 1, RepositoryId = 1, Severity = FindingSeverity.Critical }],
                    TotalCount = 1
                }
            };

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            IActionResult result = await controller.List(1, null, null, null, null, null, null, null, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueListViewModel model = viewResult.Model as IssueListViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(repository, model.Repository);
            Assert.AreEqual(1, model.PageIndex);
            Assert.AreEqual(25, model.PageSize);
            Assert.AreEqual(1, model.TotalCount);
            Assert.AreEqual(FindingSortField.Severity, model.SortField);
            Assert.IsTrue(model.SortDescending);

            Assert.IsNotNull(findings.LastQuery);
            Assert.AreEqual(1L, findings.LastQuery.RepositoryId);
            Assert.AreEqual(0, findings.LastQuery.PageIndex);
            Assert.AreEqual(25, findings.LastQuery.PageSize);
            Assert.AreEqual(FindingSortField.Severity, findings.LastQuery.SortField);
            Assert.IsTrue(findings.LastQuery.SortDescending);
        }

        [TestMethod]
        public async Task List_RespectsConfiguredPageSizeOptionsAndRejectsInvalidValues()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new();

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            _ = await controller.List(1, null, 50, null, null, null, null, null, TestContext.CancellationToken);
            Assert.AreEqual(50, findings.LastQuery.PageSize);

            _ = await controller.List(1, null, 10, null, null, null, null, null, TestContext.CancellationToken);
            Assert.AreEqual(10, findings.LastQuery.PageSize);

            _ = await controller.List(1, null, 33, null, null, null, null, null, TestContext.CancellationToken);
            Assert.AreEqual(25, findings.LastQuery.PageSize);
        }

        [TestMethod]
        public async Task List_ClampsNegativePageToFirstPage()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new();

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            IActionResult result = await controller.List(1, -3, null, null, null, null, null, null, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueListViewModel model = viewResult.Model as IssueListViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.PageIndex);
            Assert.AreEqual(0, findings.LastQuery.PageIndex);
        }

        [TestMethod]
        public async Task List_PassesFiltersSearchAndSortToQuery()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new();

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            _ = await controller.List(1, 2, 50, FindingSeverity.High, 7, "null", FindingSortField.Title, false, TestContext.CancellationToken);

            Assert.IsNotNull(findings.LastQuery);
            Assert.AreEqual(1L, findings.LastQuery.RepositoryId);
            Assert.AreEqual(1, findings.LastQuery.PageIndex);
            Assert.AreEqual(50, findings.LastQuery.PageSize);
            Assert.AreEqual(FindingSeverity.High, findings.LastQuery.Severity);
            Assert.AreEqual(7L, findings.LastQuery.ReviewTypeId);
            Assert.AreEqual("null", findings.LastQuery.SearchText);
            Assert.AreEqual(FindingSortField.Title, findings.LastQuery.SortField);
            Assert.IsFalse(findings.LastQuery.SortDescending);
        }

        [TestMethod]
        public async Task Details_ReturnsNotFoundWhenFindingMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.Details(99, null, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_ReturnsFullModelWithContext()
        {
            Repository repository = new() { Id = 1, Name = "Repo", LocalPath = @"C:\src\repo" };
            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Service.cs" };
            ReviewType reviewType = new() { Id = 100, Name = "C# Review" };
            AIReviewContext context = new() { Id = 5, ReviewInstructions = "instructions", FileContent = "content" };
            Finding finding = new()
            {
                Id = 42,
                RepositoryId = 1,
                RepoFileId = 10,
                ReviewTypeId = 100,
                ReviewContextId = 5,
                Severity = FindingSeverity.High
            };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeReviewContextRepository contexts = new();
            contexts.Contexts.Add(context);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);
            findings.Navigation = new FindingNavigation { PreviousId = 41, NextId = 43 };

            IssuesController controller = CreateController(
                repositories: repositories,
                files: files,
                reviewTypes: reviewTypes,
                contexts: contexts,
                findings: findings);

            IActionResult result = await controller.Details(42, null, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueDetailViewModel model = viewResult.Model as IssueDetailViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(finding, model.Finding);
            Assert.AreSame(repository, model.Repository);
            Assert.AreEqual("C# Review", model.ReviewTypeName);
            Assert.AreEqual("src/Service.cs", model.FileRelativePath);
            Assert.AreSame(context, model.Context);
            Assert.AreEqual(41L, model.PreviousFindingId);
            Assert.AreEqual(43L, model.NextFindingId);
        }

        [TestMethod]
        public async Task Details_ReturnsNullContextForLegacyIssue()
        {
            Repository repository = new() { Id = 1, Name = "Repo", LocalPath = @"C:\src\repo" };
            Finding finding = new() { Id = 42, RepositoryId = 1, ReviewContextId = null };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            IActionResult result = await controller.Details(42, null, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            IssueDetailViewModel model = viewResult.Model as IssueDetailViewModel;
            Assert.IsNotNull(model);
            Assert.IsNull(model.Context);
        }

        [TestMethod]
        public async Task Details_PreservesListingState()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };
            Finding finding = new() { Id = 42, RepositoryId = 1 };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            IssuesController controller = CreateController(repositories: repositories, findings: findings);

            _ = await controller.Details(42, 1, 3, 50, FindingSeverity.Medium, 7, "null", FindingSortField.Title, false, null, TestContext.CancellationToken);

            Assert.IsNotNull(findings.LastQuery);
            Assert.AreEqual(1L, findings.LastQuery.RepositoryId);
            Assert.AreEqual(2, findings.LastQuery.PageIndex);
            Assert.AreEqual(50, findings.LastQuery.PageSize);
            Assert.AreEqual(FindingSeverity.Medium, findings.LastQuery.Severity);
            Assert.AreEqual(7L, findings.LastQuery.ReviewTypeId);
            Assert.AreEqual("null", findings.LastQuery.SearchText);
            Assert.AreEqual(FindingSortField.Title, findings.LastQuery.SortField);
            Assert.IsFalse(findings.LastQuery.SortDescending);
        }

        [TestMethod]
        public async Task Export_ReturnsNotFoundWhenRepositoryMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.Export(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Export_ReturnsAllIssuesForRepositoryAsJson()
        {
            Repository repository = new() { Id = 1, Name = "Repo" };

            FakeRepositoryService repositories = new();
            repositories.Repositories.Add(repository);

            Finding older = new() { Id = 1, RepositoryId = 1, ReviewTypeId = 10, RepoFileId = 20, Title = "Older" };
            Finding newer = new() { Id = 2, RepositoryId = 1, ReviewTypeId = 11, RepoFileId = 21, Title = "Newer" };

            FakeFindingRepository findings = new();
            findings.Findings.AddRange([older, newer]);

            FakeRepositoryFileService files = new();
            files.Files.AddRange(
            [
                new RepoFile { Id = 20, RepositoryId = 1, RelativePath = @"src\A.cs" },
                new RepoFile { Id = 21, RepositoryId = 1, RelativePath = @"src\B.cs" }
            ]);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.AddRange(
            [
                new ReviewType { Id = 10, Name = "TypeA" },
                new ReviewType { Id = 11, Name = "TypeB" }
            ]);

            FakeIssueValidationService validations = new();
            validations.Validations.Add(new IssueValidation
            {
                Id = 100,
                FindingId = 2,
                RepositoryId = 1,
                Status = IssueValidationStatus.Completed,
                Verdict = IssueValidationVerdict.Agreed,
                Confidence = 0.9,
                Summary = "Agreed"
            });

            IssuesController controller = CreateController(
                repositories: repositories,
                files: files,
                findings: findings,
                reviewTypes: reviewTypes,
                validations: validations);

            IActionResult result = await controller.Export(1, TestContext.CancellationToken);

            JsonResult jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);

            IssueExportViewModel model = jsonResult.Value as IssueExportViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(repository, model.Repository);
            Assert.HasCount(2, model.Issues);

            IssueExportItemViewModel newest = model.Issues[0];
            Assert.AreEqual(2L, newest.Finding.Id);
            Assert.AreEqual("TypeB", newest.ReviewTypeName);
            Assert.AreEqual(@"src\B.cs", newest.FileRelativePath);
            Assert.IsNull(newest.Context);
            Assert.IsNotNull(newest.LatestValidation);
            Assert.AreEqual("Agreed", newest.LatestValidation.Verdict);
            Assert.AreEqual(0.9, newest.LatestValidation.Confidence.Value);

            IssueExportItemViewModel oldest = model.Issues[1];
            Assert.AreEqual(1L, oldest.Finding.Id);
            Assert.AreEqual("TypeA", oldest.ReviewTypeName);
            Assert.AreEqual(@"src\A.cs", oldest.FileRelativePath);
            Assert.IsNull(oldest.LatestValidation);
        }

        [TestMethod]
        public async Task UpdateSeverity_ReturnsNotFoundWhenFindingMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.UpdateSeverity(99, FindingSeverity.Critical, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task UpdateSeverity_UpdatesFindingAndRedirectsToDetails()
        {
            Finding finding = new() { Id = 42, RepositoryId = 1, Severity = FindingSeverity.Low };

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            IssuesController controller = CreateController(findings: findings);

            IActionResult result = await controller.UpdateSeverity(42, FindingSeverity.High, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.Details), redirect.ActionName);
            Assert.AreEqual(42L, redirect.RouteValues["id"]);
            Assert.AreEqual(1L, redirect.RouteValues["repositoryId"]);

            Assert.IsNotNull(findings.Updated);
            Assert.AreEqual(FindingSeverity.High, findings.Updated.Severity);
            Assert.AreNotEqual(default, findings.Updated.LastModifiedDate);

            Assert.AreEqual(Revathryn.Resources.Strings.SeverityUpdatedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task Delete_ReturnsNotFoundWhenFindingMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.Delete(99, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_RemovesFindingAndRedirectsToList()
        {
            Finding finding = new() { Id = 42, RepositoryId = 1 };

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            IssuesController controller = CreateController(findings: findings);

            IActionResult result = await controller.Delete(42, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.List), redirect.ActionName);
            Assert.AreEqual(1L, redirect.RouteValues["id"]);

            Assert.AreEqual(42L, findings.DeletedId);

            Assert.AreEqual(Revathryn.Resources.Strings.FindingDeletedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ResubmitForReview_ReturnsNotFoundWhenFindingMissing()
        {
            IssuesController controller = CreateController();

            IActionResult result = await controller.ResubmitForReview(99, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task ResubmitForReview_ReturnsErrorWhenFileOrReviewTypeMissing()
        {
            Finding finding = new() { Id = 42, RepositoryId = 1, RepoFileId = null, ReviewTypeId = null };

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            IssuesController controller = CreateController(findings: findings);

            IActionResult result = await controller.ResubmitForReview(42, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.Details), redirect.ActionName);
            Assert.AreEqual(42L, redirect.RouteValues["id"]);

            Assert.AreEqual(Revathryn.Resources.Strings.ResubmitUnavailableMessage, controller.TempData[Consts.ErrorMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ResubmitForReview_QueuesFileAndRedirectsToDetails()
        {
            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Service.cs" };
            ReviewType reviewType = new() { Id = 100, Name = "C# Review" };
            Finding finding = new() { Id = 42, RepositoryId = 1, RepoFileId = 10, ReviewTypeId = 100 };

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewScheduler reviewScheduler = new()
            {
                RequeueResult = new ReviewScheduleResult { Added = 1 }
            };

            IssuesController controller = CreateController(
                files: files,
                reviewTypes: reviewTypes,
                findings: findings,
                reviewScheduler: reviewScheduler);

            IActionResult result = await controller.ResubmitForReview(42, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.Details), redirect.ActionName);
            Assert.AreEqual(42L, redirect.RouteValues["id"]);

            Assert.AreEqual(10L, reviewScheduler.RequeuedRepoFileId);
            Assert.AreEqual(100L, reviewScheduler.RequeuedReviewTypeId);

            string expected = string.Format(Revathryn.Resources.Strings.ResubmitQueuedMessage, "src/Service.cs", "C# Review");
            Assert.AreEqual(expected, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ResubmitForReview_ReportsAlreadyQueuedWhenDuplicate()
        {
            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Service.cs" };
            ReviewType reviewType = new() { Id = 100, Name = "C# Review" };
            Finding finding = new() { Id = 42, RepositoryId = 1, RepoFileId = 10, ReviewTypeId = 100 };

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewScheduler reviewScheduler = new()
            {
                RequeueResult = new ReviewScheduleResult { DuplicatesSkipped = 1 }
            };

            IssuesController controller = CreateController(
                files: files,
                reviewTypes: reviewTypes,
                findings: findings,
                reviewScheduler: reviewScheduler);

            IActionResult result = await controller.ResubmitForReview(42, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.Details), redirect.ActionName);

            string expected = string.Format(Revathryn.Resources.Strings.ResubmitAlreadyQueuedMessage, "src/Service.cs", "C# Review");
            Assert.AreEqual(expected, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ResubmitForReview_ReportsErrorWhenNothingQueued()
        {
            RepoFile file = new() { Id = 10, RepositoryId = 1, RelativePath = "src/Service.cs" };
            ReviewType reviewType = new() { Id = 100, Name = "C# Review" };
            Finding finding = new() { Id = 42, RepositoryId = 1, RepoFileId = 10, ReviewTypeId = 100 };

            FakeRepositoryFileService files = new();
            files.Files.Add(file);

            FakeReviewTypeRepository reviewTypes = new();
            reviewTypes.ReviewTypes.Add(reviewType);

            FakeFindingRepository findings = new();
            findings.Findings.Add(finding);

            FakeReviewScheduler reviewScheduler = new()
            {
                RequeueResult = new ReviewScheduleResult()
            };

            IssuesController controller = CreateController(
                files: files,
                reviewTypes: reviewTypes,
                findings: findings,
                reviewScheduler: reviewScheduler);

            IActionResult result = await controller.ResubmitForReview(42, null, null, null, null, null, null, null, null, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(IssuesController.Details), redirect.ActionName);

            string expected = string.Format(Revathryn.Resources.Strings.ResubmitNotQueuedMessage, "src/Service.cs", "C# Review");
            Assert.AreEqual(expected, controller.TempData[Consts.ErrorMessageTempDataKey]);
        }

        private static IssuesController CreateController(
            FakeRepositoryService repositories = null,
            FakeRepositoryFileService files = null,
            FakeFindingRepository findings = null,
            FakeReviewTypeRepository reviewTypes = null,
            FakeReviewContextRepository contexts = null,
            FakeIssueValidationService validations = null,
            FakeReviewEngineConfigurationService reviewEngine = null,
            FakeReviewScheduler reviewScheduler = null)
        {
            FakeRepositoryService repositoryService = repositories ?? new();
            FakeRepositoryFileService fileService = files ?? new();
            FakeFindingRepository findingRepository = findings ?? new();
            FakeReviewTypeRepository reviewTypeRepository = reviewTypes ?? new();
            FakeReviewContextRepository reviewContextRepository = contexts ?? new();
            FakeIssueValidationService validationService = validations ?? new();
            FakeReviewEngineConfigurationService reviewEngineConfiguration = reviewEngine ?? new();
            FakeReviewScheduler scheduler = reviewScheduler ?? new();

            ControllerContext context = ControllerTestSupport.CreateControllerContext(out DefaultHttpContext httpContext);

            IssueSettingsProvider issueSettingsProvider = new();
            issueSettingsProvider.Refresh(new AISettings
            {
                IssueDefaultPageSize = 25,
                IssuePageSizeOptions = "10,25,50,100"
            });

            return new IssuesController(
                repositoryService,
                fileService,
                findingRepository,
                reviewTypeRepository,
                reviewContextRepository,
                validationService,
                reviewEngineConfiguration,
                issueSettingsProvider,
                scheduler)
            {
                ControllerContext = context,
                Url = new FakeUrlHelper(context),
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        private sealed class FakeReviewScheduler : IReviewScheduler
        {
            public ReviewScheduleResult RequeueResult { get; set; } = new();

            public long RequeuedRepoFileId { get; private set; }

            public long RequeuedReviewTypeId { get; private set; }

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
                RequeuedRepoFileId = repoFileId;
                RequeuedReviewTypeId = reviewTypeId;
                return Task.FromResult(RequeueResult ?? new ReviewScheduleResult());
            }

            public Task<IReadOnlyList<ReviewType>> GetApplicableReviewTypesAsync(RepoFile file, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([]);
            }

            public Task<bool> IsCurrentAsync(RepoFile file, ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }
        }

        private sealed class FakeRepositoryService : IRepositoryService
        {
            public List<Repository> Repositories { get; } = [];

            public Task<Repository> GetAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                Repository match = Repositories.FirstOrDefault(repository => repository.Id == repositoryId);
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>(Repositories);
            }

            public Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task SetEnabledAsync(long repositoryId, bool enabled, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
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
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<RepoFile>> RescanAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeFindingRepository : IFindingRepository
        {
            public List<Finding> Findings { get; } = [];

            public FindingQueryResult QueryResult { get; set; } = new();

            public FindingQuery LastQuery { get; private set; }

            public FindingNavigation Navigation { get; set; } = new();

            public Finding Updated { get; private set; }

            public long? DeletedId { get; private set; }

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
                LastQuery = query;
                return Task.FromResult(QueryResult ?? new FindingQueryResult());
            }

            public Task<FindingNavigation> GetNavigationAsync(long findingId, FindingQuery query, CancellationToken cancellationToken = default)
            {
                LastQuery = query;
                return Task.FromResult(Navigation);
            }

            public Task<IReadOnlyList<RepositoryIssueCount>> CountByRepositoryAsync(CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepositoryIssueCount> result =
                [
                    .. Findings
                        .GroupBy(finding => finding.RepositoryId)
                        .Select(group => new RepositoryIssueCount { RepositoryId = group.Key, Count = group.Count() })
                ];
                return Task.FromResult(result);
            }

            public Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
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
                throw new NotSupportedException();
            }

            public Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
            {
                Updated = finding;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                DeletedId = id;
                Findings.RemoveAll(finding => finding.Id == id);
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
                throw new NotSupportedException();
            }

            public Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
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
                throw new NotSupportedException();
            }
        }
    }
}
