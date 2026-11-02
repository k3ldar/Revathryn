using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
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

        /// <summary>Initialises a new administration controller.</summary>
        public AdministrationController(
            IAISettingsRepository aiSettingsRepository,
            IAIEngineRepository aiEngineRepository,
            IReviewUsageRepository reviewUsageRepository,
            IReviewEngineConfigurationService reviewEngineConfiguration)
        {
            _aiSettingsRepository = aiSettingsRepository;
            _aiEngineRepository = aiEngineRepository;
            _reviewUsageRepository = reviewUsageRepository;
            _reviewEngineConfiguration = reviewEngineConfiguration;
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
    }
}
