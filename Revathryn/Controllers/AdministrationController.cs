using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
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

        /// <summary>Initialises a new administration controller.</summary>
        public AdministrationController(IAISettingsRepository aiSettingsRepository, IAIEngineRepository aiEngineRepository)
        {
            _aiSettingsRepository = aiSettingsRepository;
            _aiEngineRepository = aiEngineRepository;
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
