using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Controllers;
using Revathryn.Models;
using Revathryn.Models.Administration;
using Revathryn.Resources;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class AdministrationControllerTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task CreateEngine_InvalidModelState_ReturnsViewPreservingSubmittedData()
        {
            FakeAIEngineRepository engineRepository = new();
            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Name = "My Engine",
                Model = "gpt-4o",
                Endpoint = "https://example.invalid/v1",
                SecretKeyName = "secret-key",
                Provider = AIProvider.OpenAI,
                Enabled = false,
                IsDefault = true
            };

            controller.ModelState.AddModelError(nameof(AIEngineEditViewModel.Name), "required");

            IActionResult result = await controller.CreateEngine(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);
            Assert.AreSame(model, viewResult.Model);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(4, breadcrumbs);
            Assert.AreEqual(0, engineRepository.AddCallCount);
        }

        [TestMethod]
        public async Task CreateEngine_AddAsyncThrows_ReturnsViewPreservingDataAndError()
        {
            FakeAIEngineRepository engineRepository = new()
            {
                AddException = new InvalidOperationException("storage unavailable")
            };

            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Name = "Broken Engine",
                Model = "gpt-4o"
            };

            IActionResult result = await controller.CreateEngine(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);
            Assert.AreSame(model, viewResult.Model);
            Assert.AreEqual(1, engineRepository.AddCallCount);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.HasCount(1, entry.Errors);
            Assert.AreEqual("storage unavailable", entry.Errors[0].ErrorMessage);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(4, breadcrumbs);
        }

        [TestMethod]
        public async Task CreateEngine_ValidModel_AddsEngineAndRedirectsToEngines()
        {
            FakeAIEngineRepository engineRepository = new();
            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Name = "  My Engine  ",
                Model = "  gpt-4o  ",
                Endpoint = "  https://example.invalid/v1  ",
                SecretKeyName = "  secret-key  ",
                Provider = AIProvider.AzureOpenAI,
                Enabled = true,
                IsDefault = true
            };

            IActionResult result = await controller.CreateEngine(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.Engines), redirect.ActionName);
            Assert.AreEqual(1, engineRepository.AddCallCount);

            AIEngine added = engineRepository.LastAddedEngine;
            Assert.IsNotNull(added);
            Assert.AreEqual("My Engine", added.Name);
            Assert.AreEqual("gpt-4o", added.Model);
            Assert.AreEqual("https://example.invalid/v1", added.Endpoint);
            Assert.AreEqual("secret-key", added.SecretKeyName);
            Assert.AreEqual(AIProvider.AzureOpenAI, added.Provider);
            Assert.IsTrue(added.Enabled);
            Assert.IsTrue(added.IsDefault);

            Assert.AreEqual(Strings.EngineSavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public void CreateEngine_Get_ReturnsEmptyFormModel()
        {
            AdministrationController controller = CreateController(new FakeAIEngineRepository());

            IActionResult result = controller.CreateEngine();

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);

            AIEngineEditViewModel model = viewResult.Model as AIEngineEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Id);
            Assert.AreEqual(string.Empty, model.Name);
            Assert.AreEqual(string.Empty, model.Model);
            Assert.IsTrue(model.Enabled);
        }

        [TestMethod]
        public void ToEngine_TrimsValuesAndNormalisesNulls()
        {
            AIEngineEditViewModel model = new()
            {
                Id = 42,
                Name = "  name  ",
                Model = "  model  ",
                Endpoint = null,
                SecretKeyName = null,
                Provider = AIProvider.Local,
                Enabled = true,
                IsDefault = true
            };

            AIEngine engine = model.ToEngine();

            Assert.AreEqual(42, engine.Id);
            Assert.AreEqual("name", engine.Name);
            Assert.AreEqual("model", engine.Model);
            Assert.AreEqual(string.Empty, engine.Endpoint);
            Assert.AreEqual(string.Empty, engine.SecretKeyName);
            Assert.AreEqual(AIProvider.Local, engine.Provider);
            Assert.IsTrue(engine.Enabled);
            Assert.IsTrue(engine.IsDefault);
        }

        [TestMethod]
        public void FromEngine_CopiesAllProperties()
        {
            AIEngine engine = new()
            {
                Id = 7,
                Name = "Engine",
                Provider = AIProvider.Custom,
                Endpoint = "https://example.invalid",
                Model = "gpt-4o",
                SecretKeyName = "secret",
                Enabled = false,
                IsDefault = true
            };

            AIEngineEditViewModel model = AIEngineEditViewModel.FromEngine(engine);

            Assert.AreEqual(7, model.Id);
            Assert.AreEqual("Engine", model.Name);
            Assert.AreEqual(AIProvider.Custom, model.Provider);
            Assert.AreEqual("https://example.invalid", model.Endpoint);
            Assert.AreEqual("gpt-4o", model.Model);
            Assert.AreEqual("secret", model.SecretKeyName);
            Assert.IsFalse(model.Enabled);
            Assert.IsTrue(model.IsDefault);
        }

        [TestMethod]
        public void AIEngineEditViewModel_RequiresNameAndModel()
        {
            AIEngineEditViewModel model = new();

            List<ValidationResult> results = new();
            ValidationContext context = new(model);

            bool isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

            Assert.IsFalse(isValid);
            Assert.HasCount(2, results);
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains(nameof(AIEngineEditViewModel.Name))));
            Assert.IsTrue(results.Exists(r => r.MemberNames.Contains(nameof(AIEngineEditViewModel.Model))));
        }

        [TestMethod]
        public async Task EditEngine_Get_ReturnsFormModelFromExistingEngine()
        {
            FakeAIEngineRepository engineRepository = new();
            engineRepository.Engines.Add(new AIEngine
            {
                Id = 5,
                Name = "Engine",
                Model = "gpt-4o",
                Provider = AIProvider.OpenAI,
                Endpoint = "https://example.invalid/v1",
                SecretKeyName = "secret",
                Enabled = true,
                IsDefault = true
            });

            AdministrationController controller = CreateController(engineRepository);

            IActionResult result = await controller.EditEngine(5, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);

            AIEngineEditViewModel model = viewResult.Model as AIEngineEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.Id);
            Assert.AreEqual("Engine", model.Name);
            Assert.AreEqual("gpt-4o", model.Model);
            Assert.AreEqual("https://example.invalid/v1", model.Endpoint);
            Assert.AreEqual(AIProvider.OpenAI, model.Provider);
            Assert.IsTrue(model.IsDefault);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(4, breadcrumbs);
        }

        [TestMethod]
        public async Task EditEngine_Get_NotFoundWhenMissing()
        {
            AdministrationController controller = CreateController(new FakeAIEngineRepository());

            IActionResult result = await controller.EditEngine(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task EditEngine_InvalidModelState_ReturnsViewPreservingSubmittedData()
        {
            FakeAIEngineRepository engineRepository = new();
            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Id = 5,
                Name = "Engine",
                Model = "gpt-4o"
            };

            controller.ModelState.AddModelError(nameof(AIEngineEditViewModel.Model), "required");

            IActionResult result = await controller.EditEngine(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);
            Assert.AreSame(model, viewResult.Model);
            Assert.AreEqual(0, engineRepository.UpdateCallCount);
        }

        [TestMethod]
        public async Task EditEngine_UpdateAsyncThrows_ReturnsViewPreservingDataAndError()
        {
            FakeAIEngineRepository engineRepository = new()
            {
                UpdateException = new InvalidOperationException("storage unavailable")
            };

            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Id = 5,
                Name = "Engine",
                Model = "gpt-4o"
            };

            IActionResult result = await controller.EditEngine(model, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(Consts.EngineFormViewName, viewResult.ViewName);
            Assert.AreSame(model, viewResult.Model);
            Assert.AreEqual(1, engineRepository.UpdateCallCount);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.HasCount(1, entry.Errors);
            Assert.AreEqual("storage unavailable", entry.Errors[0].ErrorMessage);
        }

        [TestMethod]
        public async Task EditEngine_ValidModel_UpdatesEngineAndRedirectsToEngines()
        {
            FakeAIEngineRepository engineRepository = new();
            AdministrationController controller = CreateController(engineRepository);

            AIEngineEditViewModel model = new()
            {
                Id = 5,
                Name = "  Engine  ",
                Model = "  gpt-4o  ",
                Endpoint = "  https://example.invalid/v1  ",
                SecretKeyName = "  secret  ",
                Provider = AIProvider.AzureOpenAI,
                Enabled = true,
                IsDefault = true
            };

            IActionResult result = await controller.EditEngine(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.Engines), redirect.ActionName);
            Assert.AreEqual(1, engineRepository.UpdateCallCount);

            AIEngine updated = engineRepository.LastUpdatedEngine;
            Assert.IsNotNull(updated);
            Assert.AreEqual(5, updated.Id);
            Assert.AreEqual("Engine", updated.Name);
            Assert.AreEqual("gpt-4o", updated.Model);
            Assert.AreEqual("https://example.invalid/v1", updated.Endpoint);
            Assert.AreEqual("secret", updated.SecretKeyName);
            Assert.AreEqual(AIProvider.AzureOpenAI, updated.Provider);

            Assert.AreEqual(Strings.EngineSavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task DeleteEngine_Get_ReturnsEngineView()
        {
            FakeAIEngineRepository engineRepository = new();
            AIEngine engine = new() { Id = 5, Name = "Engine", Model = "gpt-4o" };
            engineRepository.Engines.Add(engine);

            AdministrationController controller = CreateController(engineRepository);

            IActionResult result = await controller.DeleteEngine(5, TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreSame(engine, viewResult.Model);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(3, breadcrumbs);
        }

        [TestMethod]
        public async Task DeleteEngine_Get_NotFoundWhenMissing()
        {
            AdministrationController controller = CreateController(new FakeAIEngineRepository());

            IActionResult result = await controller.DeleteEngine(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task DeleteEngineConfirmed_DeletesAndRedirects()
        {
            FakeAIEngineRepository engineRepository = new();
            AdministrationController controller = CreateController(engineRepository);

            IActionResult result = await controller.DeleteEngineConfirmed(5, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.Engines), redirect.ActionName);
            Assert.AreEqual(5, engineRepository.DeletedId);
            Assert.AreEqual(1, engineRepository.DeleteCallCount);
            Assert.AreEqual(Strings.EngineDeletedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task ReviewUsage_ReturnsViewWithSummaryAndRecords()
        {
            FakeReviewUsageRepository usageRepository = new()
            {
                Summary = new ReviewUsageSummary { TotalReviewCount = 3, TodayReviewCount = 1 },
                Records = [new ReviewUsage { Id = 1, EngineName = "OpenAI", EngineModel = "gpt-4o" }]
            };

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), usageRepository);

            IActionResult result = await controller.ReviewUsage(TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            ReviewUsageListViewModel model = view.Model as ReviewUsageListViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Summary.TotalReviewCount);
            Assert.AreEqual(1, model.Summary.TodayReviewCount);
            Assert.HasCount(1, model.UsageRecords);
            Assert.AreEqual("OpenAI", model.UsageRecords[0].EngineName);
        }

        [TestMethod]
        public async Task ReviewTypes_NoFilter_ReturnsAllReviewTypesGroupedByRepository()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 10, Name = "Security" });
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 2, RepositoryId = 20, Name = "Style" });

            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 10, Name = "Repo A" });
            repositoryService.Repositories.Add(new Repository { Id = 20, Name = "Repo B" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.ReviewTypes(null, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            ReviewTypeListViewModel model = view.Model as ReviewTypeListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(2, model.Items);
            Assert.AreEqual("Security", model.Items[0].ReviewType.Name);
            Assert.AreEqual("Repo A", model.Items[0].RepositoryName);
            Assert.AreEqual("Style", model.Items[1].ReviewType.Name);
            Assert.AreEqual("Repo B", model.Items[1].RepositoryName);
            Assert.IsNull(model.SelectedRepositoryId);
        }

        [TestMethod]
        public async Task ReviewTypes_WithRepositoryFilter_ReturnsOnlyMatchingRepository()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 1, RepositoryId = 10, Name = "Security" });
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 2, RepositoryId = 20, Name = "Style" });

            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 10, Name = "Repo A" });
            repositoryService.Repositories.Add(new Repository { Id = 20, Name = "Repo B" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.ReviewTypes(20, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            ReviewTypeListViewModel model = view.Model as ReviewTypeListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(1, model.Items);
            Assert.AreEqual(20, model.Items[0].ReviewType.RepositoryId);
            Assert.AreEqual(20, model.SelectedRepositoryId.Value);
        }

        [TestMethod]
        public async Task ReviewTypes_NoRepositories_ReturnsEmptyItems()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            FakeRepositoryService repositoryService = new();

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.ReviewTypes(null, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            ReviewTypeListViewModel model = view.Model as ReviewTypeListViewModel;
            Assert.IsNotNull(model);
            Assert.HasCount(0, model.Items);
            Assert.HasCount(0, model.Repositories);
        }

        [TestMethod]
        public async Task CreateReviewType_Get_ReturnsEmptyFormWithRepositories()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 1, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService);

            IActionResult result = await controller.CreateReviewType((long?)null, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);

            ReviewTypeEditViewModel model = view.Model as ReviewTypeEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Id);
            Assert.AreEqual(0, model.RepositoryId);
            Assert.AreEqual(string.Empty, model.Name);
            Assert.IsTrue(model.Enabled);
            Assert.HasCount(1, model.Repositories);
        }

        [TestMethod]
        public async Task CreateReviewType_Get_WithRepositoryId_PreselectsRepository()
        {
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 7, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService);

            IActionResult result = await controller.CreateReviewType(7, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            ReviewTypeEditViewModel model = view.Model as ReviewTypeEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(7, model.RepositoryId);
        }

        [TestMethod]
        public async Task CreateReviewType_InvalidModelState_ReturnsViewPreservingSubmittedData()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 1, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new() { RepositoryId = 1, Name = "Security" };
            controller.ModelState.AddModelError(nameof(ReviewTypeEditViewModel.Name), "required");

            IActionResult result = await controller.CreateReviewType(model, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);
            Assert.AreSame(model, view.Model);
            Assert.HasCount(1, model.Repositories);
            Assert.AreEqual(0, reviewTypeRepository.AddCallCount);
        }

        [TestMethod]
        public async Task CreateReviewType_AddAsyncThrows_ReturnsViewPreservingDataAndError()
        {
            FakeReviewTypeRepository reviewTypeRepository = new()
            {
                AddException = new InvalidOperationException("storage unavailable")
            };

            FakeRepositoryService repositoryService = new();

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new() { RepositoryId = 1, Name = "Security" };

            IActionResult result = await controller.CreateReviewType(model, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);
            Assert.AreSame(model, view.Model);
            Assert.AreEqual(1, reviewTypeRepository.AddCallCount);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.HasCount(1, entry.Errors);
            Assert.AreEqual("storage unavailable", entry.Errors[0].ErrorMessage);
        }

        [TestMethod]
        public async Task CreateReviewType_ValidModel_AddsReviewTypeAndRedirects()
        {
            FakeReviewTypeRepository reviewTypeRepository = new() { NextId = 42 };
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 1, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new()
            {
                RepositoryId = 1,
                Name = "  Security  ",
                Description = "  Finds vulnerabilities  ",
                FileExtensions = "  .cs,.csproj  ",
                Prompt = "  Review for security  ",
                DefaultPriority = 3,
                Enabled = false
            };

            IActionResult result = await controller.CreateReviewType(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.ReviewTypes), redirect.ActionName);
            Assert.AreEqual(1, reviewTypeRepository.AddCallCount);

            ReviewType added = reviewTypeRepository.LastAddedReviewType;
            Assert.IsNotNull(added);
            Assert.AreEqual("Security", added.Name);
            Assert.AreEqual("Finds vulnerabilities", added.Description);
            Assert.AreEqual(".cs,.csproj", added.FileExtensions);
            Assert.AreEqual("Review for security", added.Prompt);
            Assert.AreEqual(3, added.DefaultPriority);
            Assert.IsFalse(added.Enabled);
            Assert.AreEqual(string.Empty, added.Key);
            Assert.IsFalse(string.IsNullOrEmpty(added.ReviewDefinitionVersion));
            Assert.IsGreaterThan(DateTime.MinValue, added.CreatedDate);
            Assert.IsGreaterThan(DateTime.MinValue, added.LastModifiedDate);

            Assert.AreEqual(Strings.ReviewTypeSavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task EditReviewType_Get_ReturnsFormModelFromExistingReviewType()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 5, RepositoryId = 1, Name = "Security", DefaultPriority = 2, Enabled = true });

            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 1, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.EditReviewType(5, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);

            ReviewTypeEditViewModel model = view.Model as ReviewTypeEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.Id);
            Assert.AreEqual("Security", model.Name);
            Assert.AreEqual(2, model.DefaultPriority);
            Assert.IsTrue(model.Enabled);
            Assert.HasCount(1, model.Repositories);
        }

        [TestMethod]
        public async Task EditReviewType_Get_NotFoundWhenMissing()
        {
            AdministrationController controller = CreateController(new FakeAIEngineRepository());

            IActionResult result = await controller.EditReviewType(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task EditReviewType_InvalidModelState_ReturnsViewPreservingSubmittedData()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            FakeRepositoryService repositoryService = new();
            repositoryService.Repositories.Add(new Repository { Id = 1, Name = "Repo A" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), repositoryService: repositoryService, reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new() { Id = 5, RepositoryId = 1, Name = "Security" };
            controller.ModelState.AddModelError(nameof(ReviewTypeEditViewModel.Name), "required");

            IActionResult result = await controller.EditReviewType(model, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);
            Assert.AreSame(model, view.Model);
            Assert.HasCount(1, model.Repositories);
            Assert.AreEqual(0, reviewTypeRepository.UpdateCallCount);
        }

        [TestMethod]
        public async Task EditReviewType_NotFoundWhenExistingMissing()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            AdministrationController controller = CreateController(new FakeAIEngineRepository(), reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new() { Id = 99, RepositoryId = 1, Name = "Security" };

            IActionResult result = await controller.EditReviewType(model, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task EditReviewType_UpdateAsyncThrows_ReturnsViewPreservingDataAndError()
        {
            FakeReviewTypeRepository reviewTypeRepository = new()
            {
                UpdateException = new InvalidOperationException("storage unavailable")
            };
            reviewTypeRepository.ReviewTypes.Add(new ReviewType { Id = 5, RepositoryId = 1, Name = "Security" });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new() { Id = 5, RepositoryId = 1, Name = "Security" };

            IActionResult result = await controller.EditReviewType(model, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(Consts.ReviewTypeFormViewName, view.ViewName);
            Assert.AreSame(model, view.Model);
            Assert.AreEqual(1, reviewTypeRepository.UpdateCallCount);

            ModelStateEntry entry = controller.ModelState[string.Empty];
            Assert.IsNotNull(entry);
            Assert.HasCount(1, entry.Errors);
            Assert.AreEqual("storage unavailable", entry.Errors[0].ErrorMessage);
        }

        [TestMethod]
        public async Task EditReviewType_ValidModel_UpdatesReviewTypeAndRedirects()
        {
            DateTime createdDate = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            FakeReviewTypeRepository reviewTypeRepository = new();
            reviewTypeRepository.ReviewTypes.Add(new ReviewType
            {
                Id = 5,
                RepositoryId = 1,
                Key = "builtin",
                Name = "Old",
                CreatedDate = createdDate
            });

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), reviewTypeRepository: reviewTypeRepository);

            ReviewTypeEditViewModel model = new()
            {
                Id = 5,
                RepositoryId = 1,
                Name = "  Security  ",
                Description = "  Finds vulnerabilities  ",
                FileExtensions = "  .cs  ",
                Prompt = "  Review  ",
                DefaultPriority = 4,
                Enabled = false
            };

            IActionResult result = await controller.EditReviewType(model, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.ReviewTypes), redirect.ActionName);
            Assert.AreEqual(1, reviewTypeRepository.UpdateCallCount);

            ReviewType updated = reviewTypeRepository.LastUpdatedReviewType;
            Assert.IsNotNull(updated);
            Assert.AreEqual(5, updated.Id);
            Assert.AreEqual("Security", updated.Name);
            Assert.AreEqual(4, updated.DefaultPriority);
            Assert.IsFalse(updated.Enabled);
            Assert.AreEqual("builtin", updated.Key);
            Assert.AreEqual(createdDate, updated.CreatedDate);
            Assert.IsGreaterThanOrEqualTo(createdDate, updated.LastModifiedDate);

            Assert.AreEqual(Strings.ReviewTypeSavedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        [TestMethod]
        public async Task DeleteReviewType_Get_ReturnsReviewTypeView()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            ReviewType reviewType = new() { Id = 5, Name = "Security" };
            reviewTypeRepository.ReviewTypes.Add(reviewType);

            AdministrationController controller = CreateController(new FakeAIEngineRepository(), reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.DeleteReviewType(5, TestContext.CancellationToken);

            ViewResult view = result as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(reviewType, view.Model);

            List<BreadcrumbItem> breadcrumbs = view.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(3, breadcrumbs);
        }

        [TestMethod]
        public async Task DeleteReviewType_Get_NotFoundWhenMissing()
        {
            AdministrationController controller = CreateController(new FakeAIEngineRepository());

            IActionResult result = await controller.DeleteReviewType(99, TestContext.CancellationToken);

            Assert.IsInstanceOfType<NotFoundResult>(result);
        }

        [TestMethod]
        public async Task DeleteReviewTypeConfirmed_DeletesAndRedirects()
        {
            FakeReviewTypeRepository reviewTypeRepository = new();
            AdministrationController controller = CreateController(new FakeAIEngineRepository(), reviewTypeRepository: reviewTypeRepository);

            IActionResult result = await controller.DeleteReviewTypeConfirmed(5, TestContext.CancellationToken);

            RedirectToActionResult redirect = result as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual(nameof(AdministrationController.ReviewTypes), redirect.ActionName);
            Assert.AreEqual(1, reviewTypeRepository.DeleteCallCount);
            Assert.AreEqual(5, reviewTypeRepository.DeletedId);

            Assert.AreEqual(Strings.ReviewTypeDeletedMessage, controller.TempData[Consts.SuccessMessageTempDataKey]);
        }

        private static AdministrationController CreateController(
            IAIEngineRepository engineRepository,
            FakeReviewUsageRepository usageRepository = null,
            FakeReviewEngineConfigurationService reviewEngine = null,
            FakeReviewTypeRepository reviewTypeRepository = null,
            FakeRepositoryService repositoryService = null)
        {
            DefaultHttpContext httpContext = new();

            ActionContext actionContext = new(httpContext, new RouteData(), new ControllerActionDescriptor());

            AdministrationController controller = new(
                new FakeAISettingsRepository(),
                engineRepository,
                usageRepository ?? new FakeReviewUsageRepository(),
                reviewEngine ?? new FakeReviewEngineConfigurationService(),
                reviewTypeRepository ?? new FakeReviewTypeRepository(),
                repositoryService ?? new FakeRepositoryService())
            {
                ControllerContext = new ControllerContext(actionContext),
                Url = new FakeUrlHelper(actionContext),
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };

            return controller;
        }

        private sealed class FakeAIEngineRepository : IAIEngineRepository
        {
            public List<AIEngine> Engines { get; } = new();

            public AIEngine LastAddedEngine { get; private set; }

            public AIEngine LastUpdatedEngine { get; private set; }

            public int AddCallCount { get; private set; }

            public int UpdateCallCount { get; private set; }

            public int DeleteCallCount { get; private set; }

            public long DeletedId { get; private set; }

            public Exception AddException { get; set; }

            public Exception UpdateException { get; set; }

            public long NextId { get; set; } = 1;

            public Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                AddCallCount++;
                LastAddedEngine = engine;

                if (AddException is not null)
                {
                    throw AddException;
                }

                return Task.FromResult(NextId);
            }

            public Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Engines.FirstOrDefault(engine => engine.Id == id));
            }

            public Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AIEngine>>([.. Engines]);
            }

            public Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AIEngine>>([.. Engines.Where(engine => engine.Enabled)]);
            }

            public Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Engines.FirstOrDefault(engine => engine.IsDefault));
            }

            public Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                UpdateCallCount++;
                LastUpdatedEngine = engine;

                if (UpdateException is not null)
                {
                    throw UpdateException;
                }

                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                DeleteCallCount++;
                DeletedId = id;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAISettingsRepository : IAISettingsRepository
        {
            public Task<AISettings> GetAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new AISettings());
            }

            public Task UpdateAsync(AISettings settings, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeReviewUsageRepository : IReviewUsageRepository
        {
            public ReviewUsageSummary Summary { get; set; } = new();

            public IReadOnlyList<ReviewUsage> Records { get; set; } = [];

            public Task<long> AddAsync(ReviewUsage usage, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0L);
            }

            public Task<IReadOnlyList<ReviewUsage>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Records);
            }

            public Task<ReviewUsageSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Summary);
            }
        }

        private sealed class FakeReviewTypeRepository : IReviewTypeRepository
        {
            public List<ReviewType> ReviewTypes { get; } = new();

            public ReviewType LastAddedReviewType { get; private set; }

            public ReviewType LastUpdatedReviewType { get; private set; }

            public int AddCallCount { get; private set; }

            public int UpdateCallCount { get; private set; }

            public int DeleteCallCount { get; private set; }

            public long DeletedId { get; private set; }

            public Exception AddException { get; set; }

            public Exception UpdateException { get; set; }

            public long NextId { get; set; } = 1;

            public Task<ReviewType> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(ReviewTypes.FirstOrDefault(reviewType => reviewType.Id == id));
            }

            public Task<IReadOnlyList<ReviewType>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. ReviewTypes.Where(reviewType => reviewType.RepositoryId == repositoryId)]);
            }

            public Task<IReadOnlyList<ReviewType>> GetEnabledByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ReviewType>>([.. ReviewTypes.Where(reviewType => reviewType.RepositoryId == repositoryId && reviewType.Enabled)]);
            }

            public Task<long> AddAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                AddCallCount++;
                LastAddedReviewType = reviewType;

                if (AddException is not null)
                {
                    throw AddException;
                }

                return Task.FromResult(NextId);
            }

            public Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
            {
                UpdateCallCount++;
                LastUpdatedReviewType = reviewType;

                if (UpdateException is not null)
                {
                    throw UpdateException;
                }

                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                DeleteCallCount++;
                DeletedId = id;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRepositoryService : IRepositoryService
        {
            public List<Repository> Repositories { get; } = new();

            public Task<Repository> RegisterAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                Repositories.Add(repository);
                return Task.FromResult(repository);
            }

            public Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
            {
                Repository existing = Repositories.FirstOrDefault(item => item.Id == repository.Id);

                if (existing is not null)
                {
                    Repositories.Remove(existing);
                }

                Repositories.Add(repository);
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                Repositories.RemoveAll(item => item.Id == repositoryId);
                return Task.CompletedTask;
            }

            public Task<Repository> GetAsync(long repositoryId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Repositories.FirstOrDefault(item => item.Id == repositoryId));
            }

            public Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<Repository>>([.. Repositories]);
            }

            public Task SetEnabledAsync(long repositoryId, bool enabled, CancellationToken cancellationToken = default)
            {
                Repository existing = Repositories.FirstOrDefault(item => item.Id == repositoryId);

                existing?.Enabled = enabled;

                return Task.CompletedTask;
            }
        }

        private sealed class FakeUrlHelper : IUrlHelper
        {
            private readonly ActionContext _actionContext;

            public FakeUrlHelper(ActionContext actionContext)
            {
                _actionContext = actionContext;
            }

            public ActionContext ActionContext => _actionContext;

            public string Action(UrlActionContext actionContext)
            {
                return "/test/" + actionContext.Action;
            }

            public string Content(string contentPath)
            {
                return contentPath;
            }

            public bool IsLocalUrl(string url)
            {
                return true;
            }

            public string RouteUrl(UrlRouteContext routeContext)
            {
                return "/test/route";
            }

            public string Link(string routeName, object values)
            {
                return "/test/link";
            }
        }

        private sealed class FakeTempDataProvider : ITempDataProvider
        {
            private readonly Dictionary<string, object> _data = new();

            public IDictionary<string, object> LoadTempData(HttpContext context)
            {
                return _data;
            }

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
                _data.Clear();

                foreach (KeyValuePair<string, object> pair in values)
                {
                    _data[pair.Key] = pair.Value;
                }
            }
        }
    }
}
