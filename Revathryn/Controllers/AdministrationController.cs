using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Engine;
using Revathryn.Models;
using Revathryn.Models.Administration;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>Administrative configuration operations.</summary>
    public class AdministrationController : Controller
    {
        private readonly IAISettingsRepository _aiSettingsRepository;
        private readonly IAIEngineRepository _aiEngineRepository;
        private readonly IReviewUsageRepository _reviewUsageRepository;
        private readonly IReviewEngineConfigurationService _reviewEngineConfiguration;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IRepositoryService _repositoryService;

        /// <summary>Initialises a new administration controller.</summary>
        public AdministrationController(
            IAISettingsRepository aiSettingsRepository,
            IAIEngineRepository aiEngineRepository,
            IReviewUsageRepository reviewUsageRepository,
            IReviewEngineConfigurationService reviewEngineConfiguration,
            IReviewTypeRepository reviewTypeRepository,
            IRepositoryService repositoryService)
        {
            _aiSettingsRepository = aiSettingsRepository;
            _aiEngineRepository = aiEngineRepository;
            _reviewUsageRepository = reviewUsageRepository;
            _reviewEngineConfiguration = reviewEngineConfiguration;
            _reviewTypeRepository = reviewTypeRepository;
            _repositoryService = repositoryService;
        }

        /// <summary>Renders the independent review engine configuration, separate from the detection engines.</summary>
        [HttpGet]
        public async Task<IActionResult> ReviewEngine(CancellationToken cancellationToken)
        {
            ReviewEngineSettings settings = await _reviewEngineConfiguration.GetAsync(cancellationToken);
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.ReviewEngineTitle);
            return View(ReviewEngineEditViewModel.FromSettings(settings));
        }

        /// <summary>Validates and saves the review engine configuration.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewEngine(ReviewEngineEditViewModel form, CancellationToken cancellationToken)
        {
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.ReviewEngineTitle);
            ReviewEngineValidationResult result = await _reviewEngineConfiguration.SaveAsync(form.ToSettings(), cancellationToken);

            if (!result.IsValid)
            {
                foreach (string error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(form);
            }

            TempData[Consts.SuccessMessageTempDataKey] = Strings.ReviewEngineSaved;
            return RedirectToAction(nameof(ReviewEngine));
        }

        /// <summary>Tests connectivity of the submitted review engine configuration without saving it.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestReviewEngine(ReviewEngineEditViewModel form, CancellationToken cancellationToken)
        {
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.ReviewEngineTitle);
            ReviewEngineConnectionResult result = await _reviewEngineConfiguration.TestConnectionAsync(form.ToSettings(), cancellationToken);

            TempData[result.Success ? Consts.SuccessMessageTempDataKey : Consts.ErrorMessageTempDataKey] = result.Message;
            return View(nameof(ReviewEngine), form);
        }

        /// <summary>Renders general platform settings.</summary>
        [HttpGet]
        public async Task<IActionResult> Settings(CancellationToken cancellationToken)
        {
            AISettings settings = await _aiSettingsRepository.GetAsync(cancellationToken);

            GeneralSettingsViewModel model = new()
            {
                MasterPrompt = settings.MasterPrompt,
                LastModifiedDate = settings.LastModifiedDate
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.GeneralSettingsTitle);

            return View(model);
        }

        /// <summary>Persists general platform settings.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(GeneralSettingsViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                AISettings settings = new()
                {
                    MasterPrompt = model.MasterPrompt?.Trim() ?? string.Empty,
                    LastModifiedDate = DateTime.UtcNow
                };

                await _aiSettingsRepository.UpdateAsync(settings, cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.SettingsSavedMessage;
                return RedirectToAction(nameof(Settings));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, string.Format(Strings.SettingsSaveFailedMessage, ex.Message));
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.GeneralSettingsTitle);
                return View(model);
            }
        }

        /// <summary>Lists configured AI engines.</summary>
        [HttpGet]
        public async Task<IActionResult> Engines(CancellationToken cancellationToken)
        {
            IReadOnlyList<AIEngine> engines = await _aiEngineRepository.GetAllAsync(cancellationToken);

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.AnalyserConfigurationTitle);

            return View(new AIEngineListViewModel { Engines = engines });
        }

        /// <summary>Renders the form for adding an AI engine.</summary>
        [HttpGet]
        public IActionResult CreateEngine()
        {
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.AddEngineTitle);
            return View(Consts.EngineFormViewName, new AIEngineEditViewModel());
        }

        /// <summary>Adds a new AI engine.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEngine(AIEngineEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.AddEngineTitle);
                return View(Consts.EngineFormViewName, form);
            }

            try
            {
                await _aiEngineRepository.AddAsync(form.ToEngine(), cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.EngineSavedMessage;
                return RedirectToAction(nameof(Engines));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.AddEngineTitle);
                return View(Consts.EngineFormViewName, form);
            }
        }

        /// <summary>Renders the form for editing an AI engine.</summary>
        [HttpGet]
        public async Task<IActionResult> EditEngine(long id, CancellationToken cancellationToken)
        {
            AIEngine engine = await _aiEngineRepository.GetByIdAsync(id, cancellationToken);

            if (engine is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.EditEngineTitle);
            return View(Consts.EngineFormViewName, AIEngineEditViewModel.FromEngine(engine));
        }

        /// <summary>Persists changes to an AI engine.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEngine(AIEngineEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.EditEngineTitle);
                return View(Consts.EngineFormViewName, form);
            }

            try
            {
                await _aiEngineRepository.UpdateAsync(form.ToEngine(), cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.EngineSavedMessage;
                return RedirectToAction(nameof(Engines));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEngineEditorBreadcrumbs(Strings.EditEngineTitle);
                return View(Consts.EngineFormViewName, form);
            }
        }

        /// <summary>Renders the confirmation view for AI engine deletion.</summary>
        [HttpGet]
        public async Task<IActionResult> DeleteEngine(long id, CancellationToken cancellationToken)
        {
            AIEngine engine = await _aiEngineRepository.GetByIdAsync(id, cancellationToken);

            if (engine is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.DeleteButton);
            return View(engine);
        }

        /// <summary>Deletes an AI engine.</summary>
        [HttpPost]
        [ActionName("DeleteEngine")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEngineConfirmed(long id, CancellationToken cancellationToken)
        {
            await _aiEngineRepository.DeleteAsync(id, cancellationToken);
            TempData[Consts.SuccessMessageTempDataKey] = Strings.EngineDeletedMessage;
            return RedirectToAction(nameof(Engines));
        }

        /// <summary>Lists all AI review usage records.</summary>
        [HttpGet]
        public async Task<IActionResult> ReviewUsage(CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewUsage> usageRecords = await _reviewUsageRepository.GetAllAsync(cancellationToken);
            ReviewUsageSummary summary = await _reviewUsageRepository.GetSummaryAsync(cancellationToken);

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.ReviewUsageAdministrationTitle);

            return View(new ReviewUsageListViewModel
            {
                Summary = summary,
                UsageRecords = usageRecords
            });
        }

        /// <summary>Lists review types, optionally filtered to a single repository.</summary>
        [HttpGet]
        public async Task<IActionResult> ReviewTypes(long? repositoryId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);

            List<ReviewTypeListItemViewModel> items = [];
            foreach (Repository repository in repositories)
            {
                if (repositoryId.HasValue && repositoryId.Value > 0 && repository.Id != repositoryId.Value)
                {
                    continue;
                }

                IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
                foreach (ReviewType reviewType in reviewTypes)
                {
                    items.Add(new ReviewTypeListItemViewModel { ReviewType = reviewType, RepositoryName = repository.Name });
                }
            }

            ReviewTypeListViewModel model = new()
            {
                Items = [.. items.OrderBy(item => item.RepositoryName).ThenBy(item => item.ReviewType.Name)],
                Repositories = repositories,
                SelectedRepositoryId = repositoryId
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.ReviewTypesLabel);

            return View(model);
        }

        /// <summary>Renders the form for adding a review type.</summary>
        [HttpGet]
        public async Task<IActionResult> CreateReviewType(long? repositoryId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);

            ReviewTypeEditViewModel model = new()
            {
                Repositories = repositories
            };

            if (repositoryId.HasValue && repositoryId.Value > 0)
            {
                model.RepositoryId = repositoryId.Value;
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildReviewTypeEditorBreadcrumbs(Strings.AddReviewTypeTitle);
            return View(Consts.ReviewTypeFormViewName, model);
        }

        /// <summary>Adds a new review type.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReviewType(ReviewTypeEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return await RenderCreateReviewTypeFormAsync(form, cancellationToken);
            }

            try
            {
                ReviewType reviewType = form.ToReviewType();
                reviewType.Key = string.Empty;
                reviewType.CreatedDate = DateTime.UtcNow;
                reviewType.LastModifiedDate = DateTime.UtcNow;
                reviewType.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(reviewType);

                await _reviewTypeRepository.AddAsync(reviewType, cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.ReviewTypeSavedMessage;
                return RedirectToAction(nameof(ReviewTypes));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return await RenderCreateReviewTypeFormAsync(form, cancellationToken);
            }
        }

        /// <summary>Renders the form for editing an existing review type.</summary>
        [HttpGet]
        public async Task<IActionResult> EditReviewType(long id, CancellationToken cancellationToken)
        {
            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(id, cancellationToken);

            if (reviewType is null)
            {
                return NotFound();
            }

            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);
            ReviewTypeEditViewModel model = ReviewTypeEditViewModel.FromReviewType(reviewType);
            model.Repositories = repositories;

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildReviewTypeEditorBreadcrumbs(Strings.EditReviewTypeTitle);
            return View(Consts.ReviewTypeFormViewName, model);
        }

        /// <summary>Persists changes to an existing review type.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReviewType(ReviewTypeEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return await RenderEditReviewTypeFormAsync(form, cancellationToken);
            }

            try
            {
                ReviewType existing = await _reviewTypeRepository.GetByIdAsync(form.Id, cancellationToken);

                if (existing is null)
                {
                    return NotFound();
                }

                ReviewType updated = form.ToReviewType();
                updated.Id = existing.Id;
                updated.Key = existing.Key;
                updated.CreatedDate = existing.CreatedDate;
                updated.LastModifiedDate = DateTime.UtcNow;
                updated.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(updated);

                await _reviewTypeRepository.UpdateAsync(updated, cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.ReviewTypeSavedMessage;
                return RedirectToAction(nameof(ReviewTypes));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return await RenderEditReviewTypeFormAsync(form, cancellationToken);
            }
        }

        /// <summary>Renders the confirmation view for review type deletion.</summary>
        [HttpGet]
        public async Task<IActionResult> DeleteReviewType(long id, CancellationToken cancellationToken)
        {
            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(id, cancellationToken);

            if (reviewType is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.DeleteButton);
            return View(reviewType);
        }

        /// <summary>Deletes a review type.</summary>
        [HttpPost]
        [ActionName("DeleteReviewType")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReviewTypeConfirmed(long id, CancellationToken cancellationToken)
        {
            await _reviewTypeRepository.DeleteAsync(id, cancellationToken);
            TempData[Consts.SuccessMessageTempDataKey] = Strings.ReviewTypeDeletedMessage;
            return RedirectToAction(nameof(ReviewTypes));
        }

        private List<BreadcrumbItem> BuildAdministrationBreadcrumbs(string activeTitle)
        {
            return new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.AdministrationTitle },
                new() { Title = activeTitle, IsActive = true }
            };
        }

        private List<BreadcrumbItem> BuildEngineEditorBreadcrumbs(string activeTitle)
        {
            return new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.AdministrationTitle },
                new() { Title = Strings.AnalyserConfigurationTitle, Url = Url.Action(nameof(Engines)) },
                new() { Title = activeTitle, IsActive = true }
            };
        }

        private List<BreadcrumbItem> BuildReviewTypeEditorBreadcrumbs(string activeTitle)
        {
            return new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.AdministrationTitle },
                new() { Title = Strings.ReviewTypesLabel, Url = Url.Action(nameof(ReviewTypes)) },
                new() { Title = activeTitle, IsActive = true }
            };
        }

        private async Task<IActionResult> RenderCreateReviewTypeFormAsync(ReviewTypeEditViewModel form, CancellationToken cancellationToken)
        {
            form.Repositories = await _repositoryService.GetAllAsync(cancellationToken);
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildReviewTypeEditorBreadcrumbs(Strings.AddReviewTypeTitle);
            return View(Consts.ReviewTypeFormViewName, form);
        }

        private async Task<IActionResult> RenderEditReviewTypeFormAsync(ReviewTypeEditViewModel form, CancellationToken cancellationToken)
        {
            form.Repositories = await _repositoryService.GetAllAsync(cancellationToken);
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildReviewTypeEditorBreadcrumbs(Strings.EditReviewTypeTitle);
            return View(Consts.ReviewTypeFormViewName, form);
        }
    }
}
