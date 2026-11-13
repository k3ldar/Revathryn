using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Controllers;
using Revathryn.Models;
using Revathryn.Models.Repositories;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RepositoryControllerTests
    {
        private readonly string _missingPath = Path.Combine(Path.GetTempPath(), "revathryn-tests", Guid.NewGuid().ToString());

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Index_GroupsScansAndFlagsRepositoryProblems()
        {
            FakeRepositoryService repositoryService = new();
            FakeRepositoryScanRepository scanRepository = new();

            Repository missingPathRepository = new() { Id = 1, Name = "Missing", LocalPath = _missingPath };
            Repository failedScanRepository = new() { Id = 2, Name = "Failed", LocalPath = AppContext.BaseDirectory };
            Repository healthyRepository = new() { Id = 3, Name = "Healthy", LocalPath = AppContext.BaseDirectory };

            repositoryService.Repositories.Add(missingPathRepository);
            repositoryService.Repositories.Add(failedScanRepository);
            repositoryService.Repositories.Add(healthyRepository);

            scanRepository.Records.Add(new RepositoryScanRecord { Id = 10, RepositoryId = 1, Succeeded = true });
            scanRepository.Records.Add(new RepositoryScanRecord { Id = 20, RepositoryId = 2, Succeeded = false });
            scanRepository.Records.Add(new RepositoryScanRecord { Id = 30, RepositoryId = 3, Succeeded = true });

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), scanRepository, new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryListViewModel model = viewResult.Model as RepositoryListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(3, model.Repositories);

            RepositoryListItemViewModel missing = model.Repositories[0];
            Assert.IsTrue(missing.PathMissing);
            Assert.IsTrue(missing.HasProblems);

            RepositoryListItemViewModel failed = model.Repositories[1];
            Assert.IsFalse(failed.PathMissing);
            Assert.IsTrue(failed.HasProblems);
            Assert.IsNotNull(failed.LastScan);
            Assert.IsFalse(failed.LastScan.Succeeded);

            RepositoryListItemViewModel healthy = model.Repositories[2];
            Assert.IsFalse(healthy.PathMissing);
            Assert.IsFalse(healthy.HasProblems);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(2, breadcrumbs);
        }

        [TestMethod]
        public async Task Create_Get_ReturnsEmptyForm()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Create(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryEditViewModel model = viewResult.Model as RepositoryEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Id);
            Assert.AreEqual(string.Empty, model.Name);
            Assert.AreEqual(string.Empty, model.LocalPath);
            Assert.IsTrue(model.Enabled);
        }

        [TestMethod]
        public async Task Create_InvalidModel_ReturnsViewPreservingData()
        {
            FakeRepositoryService repositoryService = new();
            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new()
            {
                Name = "My Repo",
                LocalPath = AppContext.BaseDirectory,
                MaxContextRequests = 5
            };

            controller.ModelState.AddModelError(nameof(RepositoryEditViewModel.Name), "required");

            IActionResult result = await controller.Create(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.IsNull(viewResult.ViewName);
            Assert.AreSame(model, viewResult.Model);
            Assert.IsNull(repositoryService.Registered);
        }

        [TestMethod]
        public async Task Create_ValidModel_RegistersAndRedirects()
        {
            FakeRepositoryService repositoryService = new();
            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new()
            {
                Name = "  My Repo  ",
                Description = "  description  ",
                LocalPath = "  C:\\repo  ",
                MaxContextRequests = 5,
                Enabled = true,
                AutomaticReviewEnabled = false
            };

            IActionResult result = await controller.Create(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Index), redirect.ActionName);

            Repository registered = repositoryService.Registered;
            Assert.IsNotNull(registered);
            Assert.AreEqual("My Repo", registered.Name);
            Assert.AreEqual("description", registered.Description);
            Assert.AreEqual("C:\\repo", registered.LocalPath);
            Assert.AreEqual(5, registered.MaxContextRequests);
            Assert.IsFalse(registered.AutomaticReviewEnabled);

            Assert.AreEqual(Revathryn.Resources.Strings.RepositorySavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task Create_RegisterThrows_ReturnsViewPreservingDataAndError()
        {
            FakeRepositoryService repositoryService = new()
            {
                RegisterException = new ArgumentException("invalid path")
            };

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new() { Name = "Broken" };

            IActionResult result = await controller.Create(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(model, viewResult.Model);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.HasCount(1, entry.Errors);
            Assert.AreEqual("invalid path", entry.Errors[0].ErrorMessage);
        }

        [TestMethod]
        public async Task Edit_Get_NotFoundWhenMissing()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Edit(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Edit_Get_ReturnsFormWhenFound()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 7, Name = "Repo", LocalPath = AppContext.BaseDirectory, MaxContextRequests = 4 });

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Edit(7, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryEditViewModel model = viewResult.Model as RepositoryEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(7, model.Id);
            Assert.AreEqual("Repo", model.Name);
            Assert.AreEqual(4, model.MaxContextRequests);
        }

        [TestMethod]
        public async Task Edit_InvalidModel_ReturnsViewPreservingData()
        {
            FakeRepositoryService repositoryService = new();
            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new() { Id = 9, Name = "Changed" };
            controller.ModelState.AddModelError(nameof(RepositoryEditViewModel.LocalPath), "required");

            IActionResult result = await controller.Edit(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(model, viewResult.Model);
            Assert.IsNull(repositoryService.Updated);
        }

        [TestMethod]
        public async Task Edit_ValidModel_UpdatesAndRedirects()
        {
            FakeRepositoryService repositoryService = new();
            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new() { Id = 8, Name = "  Updated  ", LocalPath = AppContext.BaseDirectory, MaxContextRequests = 6 };

            IActionResult result = await controller.Edit(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Index), redirect.ActionName);

            Repository updated = repositoryService.Updated;
            Assert.IsNotNull(updated);
            Assert.AreEqual(8, updated.Id);
            Assert.AreEqual("Updated", updated.Name);

            Assert.AreEqual(Revathryn.Resources.Strings.RepositorySavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task Edit_UpdateThrows_ReturnsViewPreservingDataAndError()
        {
            FakeRepositoryService repositoryService = new()
            {
                UpdateException = new InvalidOperationException("cannot update")
            };

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            RepositoryEditViewModel model = new() { Id = 8, Name = "Keep Me" };

            IActionResult result = await controller.Edit(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(model, viewResult.Model);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.AreEqual("cannot update", entry.Errors[0].ErrorMessage);
        }

        [TestMethod]
        public async Task Details_NotFoundWhenMissing()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Details(5, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Details_ReturnsManagementViewModelWhenFound()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 4, Name = "Repo", LocalPath = AppContext.BaseDirectory });

            FakeRepositoryScanRepository scanRepository = new();
            RepositoryScanRecord newest = new() { Id = 1, RepositoryId = 4, Succeeded = true };
            RepositoryScanRecord older = new() { Id = 2, RepositoryId = 4, Succeeded = false };
            scanRepository.Records.Add(newest);
            scanRepository.Records.Add(older);

            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 1, Name = "Review" });

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), scanRepository, reviewTypeRepository, out _);

            IActionResult result = await controller.Details(4, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            RepositoryManagementDetailsViewModel model = viewResult.Model as RepositoryManagementDetailsViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual("Repo", model.Repository.Name);
            Assert.HasCount(2, model.Scans);
            Assert.HasCount(1, model.ReviewTypes);
            Assert.IsFalse(model.PathMissing);
            Assert.AreSame(newest, model.LastScan);
        }

        [TestMethod]
        public async Task ToggleReviewType_EnablesReviewTypeAndRedirects()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 7, Name = "Review" });

            FakeRepositoryReviewRepository repositoryReviewRepository = new();

            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), reviewTypeRepository, out _, repositoryReviewRepository: repositoryReviewRepository);

            IActionResult result = await controller.ToggleReviewType(4, 7, true, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Details), redirect.ActionName);
            Assert.AreEqual(4L, redirect.RouteValues["id"]);
            Assert.AreEqual(1, repositoryReviewRepository.SetEnabledCallCount);
            Assert.AreEqual(4, repositoryReviewRepository.LastRepositoryId);
            Assert.AreEqual(7, repositoryReviewRepository.LastReviewTypeId);
            Assert.IsTrue(repositoryReviewRepository.LastEnabled);
            Assert.AreEqual(Revathryn.Resources.Strings.ReviewTypeEnabledMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ToggleReviewType_DisablesReviewTypeAndRedirects()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 8, Name = "Review" });

            FakeRepositoryReviewRepository repositoryReviewRepository = new();

            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), reviewTypeRepository, out _, repositoryReviewRepository: repositoryReviewRepository);

            IActionResult result = await controller.ToggleReviewType(4, 8, false, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Details), redirect.ActionName);
            Assert.AreEqual(4L, redirect.RouteValues["id"]);
            Assert.AreEqual(1, repositoryReviewRepository.SetEnabledCallCount);
            Assert.AreEqual(4, repositoryReviewRepository.LastRepositoryId);
            Assert.AreEqual(8, repositoryReviewRepository.LastReviewTypeId);
            Assert.IsFalse(repositoryReviewRepository.LastEnabled);
            Assert.AreEqual(Revathryn.Resources.Strings.ReviewTypeDisabledMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ToggleReviewType_NotFoundWhenReviewTypeMissing()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.ToggleReviewType(4, 999, true, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Scan_NotFoundWhenMissing()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Scan(11, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Scan_ProcessesAndSetsSuccessMessage()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 12, Name = "Repo", LocalPath = AppContext.BaseDirectory });

            FakeRepositoryProcessor processor = new()
            {
                Summary = new RepositoryScanSummary { FilesDiscovered = 10, NewFiles = 3, ChangedFiles = 2 }
            };

            RepositoryController controller = CreateController(repositoryService, processor, new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Scan(12, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Details), redirect.ActionName);
            Assert.AreEqual(12L, redirect.RouteValues["id"]);
            Assert.AreEqual(1, processor.ProcessCallCount);
            Assert.AreEqual(12, processor.ProcessedId);

            object message = controller.TempData[Consts.SuccessMessageTempDataKey];
            Assert.IsNotNull(message);
            Assert.IsFalse(string.IsNullOrEmpty(message as string));
        }

        [TestMethod]
        public async Task Scan_SetsErrorMessageWhenProcessorThrows()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 13, Name = "Repo", LocalPath = AppContext.BaseDirectory });

            FakeRepositoryProcessor processor = new()
            {
                ProcessException = new InvalidOperationException("scan failed")
            };

            RepositoryController controller = CreateController(repositoryService, processor, new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Scan(13, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Details), redirect.ActionName);
            Assert.AreEqual(13L, redirect.RouteValues["id"]);

            object message = controller.TempData[Consts.ErrorMessageTempDataKey];
            Assert.IsNotNull(message);
            Assert.IsFalse(string.IsNullOrEmpty(message as string));
        }

        [TestMethod]
        public async Task Delete_Get_NotFoundWhenMissing()
        {
            RepositoryController controller = CreateController(new FakeRepositoryService(), new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Delete(21, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task Delete_Get_ReturnsConfirmationView()
        {
            FakeRepositoryService repositoryService = new();
            Repository repository = new() { Id = 22, Name = "Doomed" };
            repositoryService.Repositories.Add(repository);

            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.Delete(22, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(repository, viewResult.Model);
        }

        [TestMethod]
        public async Task Delete_ConfirmedDeletesAndRedirects()
        {
            FakeRepositoryService repositoryService = new();
            RepositoryController controller = CreateController(repositoryService, new FakeRepositoryProcessor(), new FakeRepositoryScanRepository(), new FakeReviewTypeRepository(), out _);

            IActionResult result = await controller.DeleteConfirmed(23, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(RepositoryController.Index), redirect.ActionName);
            Assert.IsTrue(repositoryService.DeleteCalled);
            Assert.AreEqual(23, repositoryService.DeletedId);
            Assert.AreEqual(Revathryn.Resources.Strings.RepositoryDeletedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        private static RepositoryController CreateController(
            FakeRepositoryService repositoryService,
            FakeRepositoryProcessor processor,
            FakeRepositoryScanRepository scanRepository,
            FakeReviewTypeRepository reviewTypeRepository,
            out DefaultHttpContext httpContext,
            FakeAIEngineRepository aiEngineRepository = null,
            FakeRepositoryReviewRepository repositoryReviewRepository = null)
        {
            ControllerContext context = ControllerTestSupport.CreateControllerContext(out httpContext);

            return new RepositoryController(
                repositoryService,
                processor,
                scanRepository,
                reviewTypeRepository,
                repositoryReviewRepository ?? new FakeRepositoryReviewRepository(),
                aiEngineRepository ?? new FakeAIEngineRepository())
            {
                ControllerContext = context,
                Url = new FakeUrlHelper(context),
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        private sealed class FakeRepositoryService : IRepositoryService
        {
            public List<Repository> Repositories { get; } = new();

            public Repository Registered { get; private set; }

            public Repository Updated { get; private set; }

            public long DeletedId { get; private set; }

            public bool DeleteCalled { get; private set; }

            public Exception RegisterException { get; set; }

            public Exception UpdateException { get; set; }

            public Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                if (RegisterException is not null)
                {
                    throw RegisterException;
                }

                Registered = repository;
                Repositories.Add(repository);
                return Task.FromResult(repository);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                if (UpdateException is not null)
                {
                    throw UpdateException;
                }

                Updated = repository;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                DeleteCalled = true;
                DeletedId = repositoryId;
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

        private sealed class FakeRepositoryProcessor : IRepositoryProcessor
        {
            public long ProcessedId { get; private set; }

            public int ProcessCallCount { get; private set; }

            public RepositoryScanSummary Summary { get; set; } = new();

            public Exception ProcessException { get; set; }

            public Task<RepositoryScanSummary> ProcessAsync(long repositoryId, CancellationToken cancellationToken, IProgress<RepositoryScanProgress> progress = null, bool forceFullScan = false)
            {
                ProcessCallCount++;
                ProcessedId = repositoryId;

                if (ProcessException is not null)
                {
                    throw ProcessException;
                }

                return Task.FromResult(Summary);
            }

            public Task<IReadOnlyList<RepositoryScanSummary>> ProcessAllEnabledAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanSummary>>([]);
            }
        }

        private sealed class FakeRepositoryScanRepository : IRepositoryScanRepository
        {
            public List<RepositoryScanRecord> Records { get; } = new();

            public Task<long> AddAsync(RepositoryScanRecord record, CancellationToken cancellationToken = default)
            {
                Records.Add(record);
                return Task.FromResult((long)Records.Count);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<RepositoryScanRecord> result = [.. Records.Where(record => record.RepositoryId == repositoryId)];
                return Task.FromResult(result);
            }

            public Task<IReadOnlyList<RepositoryScanRecord>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryScanRecord>>(Records);
            }
        }

        private sealed class FakeReviewTypeRepository : IReviewTypeRepository
        {
            public List<ReviewType> ReviewTypes { get; } = new();

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
                ReviewTypes.Add(reviewType);
                return Task.FromResult((long)ReviewTypes.Count);
            }

            public Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                int index = ReviewTypes.FindIndex(item => item.Id == reviewType.Id);

                if (index >= 0)
                {
                    ReviewTypes[index] = reviewType;
                }

                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepositoryReviewRepository : IRepositoryReviewRepository
        {
            public long LastRepositoryId { get; private set; }

            public long LastReviewTypeId { get; private set; }

            public bool LastEnabled { get; private set; }

            public int SetEnabledCallCount { get; private set; }

            public Task<RepositoryReview> GetAsync(long repositoryId, long reviewTypeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<RepositoryReview>(null);
            }

            public Task<IReadOnlyList<RepositoryReview>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<RepositoryReview>>([]);
            }

            public Task<long> AddAsync(RepositoryReview link, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(link.Id);
            }

            public Task SetEnabledAsync(long repositoryId, long reviewTypeId, bool enabled, CancellationToken cancellationToken = default)
            {
                LastRepositoryId = repositoryId;
                LastReviewTypeId = reviewTypeId;
                LastEnabled = enabled;
                SetEnabledCallCount++;
                return Task.CompletedTask;
            }

            public Task<int> LinkAllReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }

            public Task<int> LinkToAllRepositoriesAsync(long reviewTypeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }
        }

        private sealed class FakeAIEngineRepository : IAIEngineRepository
        {
            public List<AIEngine> Engines { get; } = new();

            public Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                AIEngine match = Engines.FirstOrDefault(engine => engine.Id == id);
                return Task.FromResult(match);
            }

            public Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AIEngine>>([.. Engines]);
            }

            public Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                IReadOnlyList<AIEngine> result = [.. Engines.Where(engine => engine.Enabled)];
                return Task.FromResult(result);
            }

            public Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default)
            {
                AIEngine match = Engines.FirstOrDefault(engine => engine.IsDefault);
                return Task.FromResult(match);
            }

            public Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                Engines.Add(engine);
                return Task.FromResult((long)Engines.Count);
            }

            public Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
