using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Models;
using Revathryn.Models.Repositories;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>Repository management operations.</summary>
    public class RepositoryController : Controller
    {
        private readonly IRepositoryService _repositoryService;
        private readonly IRepositoryProcessor _repositoryProcessor;
        private readonly IRepositoryScanRepository _scanRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IAIEngineRepository _aiEngineRepository;

        /// <summary>Initialises a new repository controller.</summary>
        public RepositoryController(
            IRepositoryService repositoryService,
            IRepositoryProcessor repositoryProcessor,
            IRepositoryScanRepository scanRepository,
            IReviewTypeRepository reviewTypeRepository,
            IAIEngineRepository aiEngineRepository)
        {
            _repositoryService = repositoryService;
            _repositoryProcessor = repositoryProcessor;
            _scanRepository = scanRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _aiEngineRepository = aiEngineRepository;
        }

        /// <summary>Lists configured repositories and their current operational state.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);
            IReadOnlyList<RepositoryScanRecord> scans = await _scanRepository.GetAllAsync(cancellationToken);

            Dictionary<long, RepositoryScanRecord> lastScans = [];
            foreach (IGrouping<long, RepositoryScanRecord> group in scans.GroupBy(scan => scan.RepositoryId))
            {
                lastScans[group.Key] = group.First();
            }

            List<RepositoryListItemViewModel> items = [];
            foreach (Repository repository in repositories)
            {
                lastScans.TryGetValue(repository.Id, out RepositoryScanRecord lastScan);
                bool pathMissing = !Directory.Exists(repository.LocalPath);

                items.Add(new RepositoryListItemViewModel
                {
                    Repository = repository,
                    LastScan = lastScan,
                    PathMissing = pathMissing,
                    HasProblems = pathMissing || (lastScan is not null && !lastScan.Succeeded)
                });
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.RepositoryManagementTitle, IsActive = true }
            };

            return View(new RepositoryListViewModel { Repositories = items });
        }

        /// <summary>Renders the form for registering a new repository.</summary>
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            RepositoryEditViewModel model = new();
            await PopulateEngineOptionsAsync(model, cancellationToken);
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.AddRepositoryTitle);
            return View(model);
        }

        /// <summary>Registers a new repository.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RepositoryEditViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateEngineOptionsAsync(model, cancellationToken);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.AddRepositoryTitle);
                return View(model);
            }

            try
            {
                await _repositoryService.RegisterAsync(model.ToRepository(), cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.RepositorySavedMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateEngineOptionsAsync(model, cancellationToken);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.AddRepositoryTitle);
                return View(model);
            }
        }

        /// <summary>Renders the form for editing an existing repository.</summary>
        [HttpGet]
        public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.EditRepositoryTitle);
            RepositoryEditViewModel model = RepositoryEditViewModel.FromRepository(repository);
            await PopulateEngineOptionsAsync(model, cancellationToken);
            return View(model);
        }

        /// <summary>Persists changes to an existing repository.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RepositoryEditViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                await PopulateEngineOptionsAsync(model, cancellationToken);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.EditRepositoryTitle);
                return View(model);
            }

            try
            {
                await _repositoryService.UpdateAsync(model.ToRepository(), cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.RepositorySavedMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateEngineOptionsAsync(model, cancellationToken);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildEditorBreadcrumbs(Strings.EditRepositoryTitle);
                return View(model);
            }
        }

        /// <summary>Renders repository details, scan history and review types.</summary>
        [HttpGet]
        public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            IReadOnlyList<RepositoryScanRecord> scans = await _scanRepository.GetByRepositoryAsync(id, cancellationToken);
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(id, cancellationToken);

            RepositoryManagementDetailsViewModel model = new()
            {
                Repository = repository,
                Scans = scans,
                ReviewTypes = reviewTypes,
                PathMissing = !Directory.Exists(repository.LocalPath),
                LastScan = scans.FirstOrDefault()
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.RepositoryManagementTitle, Url = Url.Action(nameof(Index)) },
                new() { Title = repository.Name, IsActive = true }
            };

            return View(model);
        }

        /// <summary>Initiates a repository scan and reports the outcome.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Scan(long id, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            try
            {
                RepositoryScanSummary summary = await _repositoryProcessor.ProcessAsync(id, cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = string.Format(Strings.RepositoryScanCompletedMessage, summary.FilesDiscovered, summary.NewFiles, summary.ChangedFiles);
            }
            catch (Exception ex)
            {
                TempData[Consts.ErrorMessageTempDataKey] = string.Format(Strings.RepositoryScanFailedMessage, ex.Message);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>Renders the confirmation view for repository deletion.</summary>
        [HttpGet]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.RepositoryManagementTitle, Url = Url.Action(nameof(Index)) },
                new() { Title = Strings.DeleteButton, IsActive = true }
            };

            return View(repository);
        }

        /// <summary>Deletes a repository and its dependent data.</summary>
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id, CancellationToken cancellationToken)
        {
            await _repositoryService.DeleteAsync(id, cancellationToken);
            TempData[Consts.SuccessMessageTempDataKey] = Strings.RepositoryDeletedMessage;
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Toggles whether a review type is active for the repository.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReviewType(long id, long reviewTypeId, bool enabled, CancellationToken cancellationToken)
        {
            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(reviewTypeId, cancellationToken);

            if (reviewType is null || reviewType.RepositoryId != id)
            {
                return NotFound();
            }

            reviewType.Enabled = enabled;
            reviewType.LastModifiedDate = DateTime.UtcNow;

            await _reviewTypeRepository.UpdateAsync(reviewType, cancellationToken);

            TempData[Consts.SuccessMessageTempDataKey] = enabled ? Strings.ReviewTypeEnabledMessage : Strings.ReviewTypeDisabledMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateEngineOptionsAsync(RepositoryEditViewModel model, CancellationToken cancellationToken)
        {
            model.AvailableEngines = await _aiEngineRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        private List<BreadcrumbItem> BuildEditorBreadcrumbs(string activeTitle)
        {
            return new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.RepositoryManagementTitle, Url = Url.Action(nameof(Index)) },
                new() { Title = activeTitle, IsActive = true }
            };
        }
    }
}
