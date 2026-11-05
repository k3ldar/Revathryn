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
        private readonly IIssueSettingsProvider _issueSettingsProvider;
        private readonly ISeverityRuleRepository _severityRuleRepository;
        private readonly ISeverityRuleProvider _severityRuleProvider;

        /// <summary>Initialises a new administration controller.</summary>
        public AdministrationController(
            IAISettingsRepository aiSettingsRepository,
            IAIEngineRepository aiEngineRepository,
            IReviewUsageRepository reviewUsageRepository,
            IReviewEngineConfigurationService reviewEngineConfiguration,
            IReviewTypeRepository reviewTypeRepository,
            IRepositoryService repositoryService,
            IIssueSettingsProvider issueSettingsProvider,
            ISeverityRuleRepository severityRuleRepository,
            ISeverityRuleProvider severityRuleProvider)
        {
            _aiSettingsRepository = aiSettingsRepository;
            _aiEngineRepository = aiEngineRepository;
            _reviewUsageRepository = reviewUsageRepository;
            _reviewEngineConfiguration = reviewEngineConfiguration;
            _reviewTypeRepository = reviewTypeRepository;
            _repositoryService = repositoryService;
            _issueSettingsProvider = issueSettingsProvider;
            _severityRuleRepository = severityRuleRepository;
            _severityRuleProvider = severityRuleProvider;
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

            string pageSizeOptions = settings.IssuePageSizeOptions;

            if (string.IsNullOrWhiteSpace(pageSizeOptions))
            {
                pageSizeOptions = IssuePageSizeOptionsFormatter.Format(new IssueViewingOptions().PageSizeOptions);
            }

            GeneralSettingsViewModel model = new()
            {
                MasterPrompt = settings.MasterPrompt,
                LastModifiedDate = settings.LastModifiedDate,
                IssueSimilarityEnabled = settings.IssueSimilarityEnabled,
                IssueSimilarityThresholdPercent = (int)Math.Round(settings.IssueSimilarityThreshold * Consts.IssueSimilarityThresholdPercentageScale),
                IssueSimilarityMinimumTokenLength = settings.IssueSimilarityMinimumTokenLength,
                IssueDefaultPageSize = settings.IssueDefaultPageSize,
                IssuePageSizeOptions = pageSizeOptions
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.GeneralSettingsTitle);

            return View(model);
        }

        /// <summary>Persists general platform settings.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(GeneralSettingsViewModel model, CancellationToken cancellationToken)
        {
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.GeneralSettingsTitle);

            if (!TryValidateIssueSettings(model))
            {
                return View(model);
            }

            try
            {
                List<int> pageSizeOptions = IssuePageSizeOptionsFormatter.Parse(model.IssuePageSizeOptions);

                AISettings settings = new()
                {
                    MasterPrompt = model.MasterPrompt?.Trim() ?? string.Empty,
                    LastModifiedDate = DateTime.UtcNow,
                    IssueSimilarityEnabled = model.IssueSimilarityEnabled,
                    IssueSimilarityThreshold = model.IssueSimilarityThresholdPercent / Consts.IssueSimilarityThresholdPercentageScale,
                    IssueSimilarityMinimumTokenLength = model.IssueSimilarityMinimumTokenLength,
                    IssueDefaultPageSize = model.IssueDefaultPageSize,
                    IssuePageSizeOptions = IssuePageSizeOptionsFormatter.Format(pageSizeOptions)
                };

                await _aiSettingsRepository.UpdateAsync(settings, cancellationToken);
                _issueSettingsProvider.Refresh(settings);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.SettingsSavedMessage;
                return RedirectToAction(nameof(Settings));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, string.Format(Strings.SettingsSaveFailedMessage, ex.Message));
                return View(model);
            }
        }

        private bool TryValidateIssueSettings(GeneralSettingsViewModel model)
        {
            bool valid = true;

            if (model.IssueSimilarityThresholdPercent < Consts.MinimumIssueSimilarityThresholdPercent
                || model.IssueSimilarityThresholdPercent > Consts.MaximumIssueSimilarityThresholdPercent)
            {
                ModelState.AddModelError(
                    nameof(GeneralSettingsViewModel.IssueSimilarityThresholdPercent),
                    string.Format(Strings.IssueSimilarityThresholdPercentRangeMessage, Consts.MinimumIssueSimilarityThresholdPercent, Consts.MaximumIssueSimilarityThresholdPercent));
                valid = false;
            }

            if (model.IssueSimilarityMinimumTokenLength < Consts.MinimumIssueSimilarityTokenLength
                || model.IssueSimilarityMinimumTokenLength > Consts.MaximumIssueSimilarityTokenLength)
            {
                ModelState.AddModelError(
                    nameof(GeneralSettingsViewModel.IssueSimilarityMinimumTokenLength),
                    string.Format(Strings.IssueSimilarityMinimumTokenLengthRangeMessage, Consts.MinimumIssueSimilarityTokenLength, Consts.MaximumIssueSimilarityTokenLength));
                valid = false;
            }

            if (model.IssueDefaultPageSize < Consts.MinimumIssuePageSize
                || model.IssueDefaultPageSize > Consts.MaximumIssuePageSize)
            {
                ModelState.AddModelError(
                    nameof(GeneralSettingsViewModel.IssueDefaultPageSize),
                    string.Format(Strings.IssueDefaultPageSizeRangeMessage, Consts.MinimumIssuePageSize, Consts.MaximumIssuePageSize));
                valid = false;
            }

            List<int> pageSizeOptions = IssuePageSizeOptionsFormatter.Parse(model.IssuePageSizeOptions);

            if (pageSizeOptions.Count == 0 || pageSizeOptions.Count > Consts.MaximumIssuePageSizeOptionCount)
            {
                ModelState.AddModelError(
                    nameof(GeneralSettingsViewModel.IssuePageSizeOptions),
                    string.Format(Strings.IssuePageSizeOptionsInvalidMessage, Consts.MinimumIssuePageSize, Consts.MaximumIssuePageSize, Consts.MaximumIssuePageSizeOptionCount));
                valid = false;
            }
            else if (!pageSizeOptions.Contains(model.IssueDefaultPageSize))
            {
                ModelState.AddModelError(
                    nameof(GeneralSettingsViewModel.IssueDefaultPageSize),
                    Strings.IssueDefaultPageSizeMustBeOptionMessage);
                valid = false;
            }

            return valid;
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

        /// <summary>Lists configured severity override rules in evaluation order.</summary>
        [HttpGet]
        public async Task<IActionResult> SeverityRules(CancellationToken cancellationToken)
        {
            IReadOnlyList<SeverityRule> rules = await _severityRuleRepository.GetAllAsync(cancellationToken);

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.SeverityRulesLabel);

            return View(new SeverityRuleListViewModel { Rules = rules });
        }

        /// <summary>Renders the form for adding a severity override rule.</summary>
        [HttpGet]
        public IActionResult CreateSeverityRule()
        {
            ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.AddSeverityRuleTitle);
            return View(Consts.SeverityRuleFormViewName, new SeverityRuleEditViewModel());
        }

        /// <summary>Adds a new severity override rule.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSeverityRule(SeverityRuleEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.AddSeverityRuleTitle);
                return View(Consts.SeverityRuleFormViewName, form);
            }

            try
            {
                SeverityRule rule = form.ToRule();
                rule.CreatedDate = DateTime.UtcNow;
                rule.LastModifiedDate = DateTime.UtcNow;

                await _severityRuleRepository.AddAsync(rule, cancellationToken);
                await RefreshSeverityRulesAsync(cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.SeverityRuleSavedMessage;
                return RedirectToAction(nameof(SeverityRules));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.AddSeverityRuleTitle);
                return View(Consts.SeverityRuleFormViewName, form);
            }
        }

        /// <summary>Renders the form for editing a severity override rule.</summary>
        [HttpGet]
        public async Task<IActionResult> EditSeverityRule(long id, CancellationToken cancellationToken)
        {
            SeverityRule rule = await _severityRuleRepository.GetByIdAsync(id, cancellationToken);

            if (rule is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.EditSeverityRuleTitle);
            return View(Consts.SeverityRuleFormViewName, SeverityRuleEditViewModel.FromRule(rule));
        }

        /// <summary>Persists changes to a severity override rule.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSeverityRule(SeverityRuleEditViewModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.EditSeverityRuleTitle);
                return View(Consts.SeverityRuleFormViewName, form);
            }

            try
            {
                SeverityRule existing = await _severityRuleRepository.GetByIdAsync(form.Id, cancellationToken);

                if (existing is null)
                {
                    return NotFound();
                }

                SeverityRule updated = form.ToRule();
                updated.Id = existing.Id;
                updated.CreatedDate = existing.CreatedDate;
                updated.LastModifiedDate = DateTime.UtcNow;

                await _severityRuleRepository.UpdateAsync(updated, cancellationToken);
                await RefreshSeverityRulesAsync(cancellationToken);
                TempData[Consts.SuccessMessageTempDataKey] = Strings.SeverityRuleSavedMessage;
                return RedirectToAction(nameof(SeverityRules));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewData[Consts.BreadcrumbsViewDataKey] = BuildSeverityRuleEditorBreadcrumbs(Strings.EditSeverityRuleTitle);
                return View(Consts.SeverityRuleFormViewName, form);
            }
        }

        /// <summary>Renders the confirmation view for severity override rule deletion.</summary>
        [HttpGet]
        public async Task<IActionResult> DeleteSeverityRule(long id, CancellationToken cancellationToken)
        {
            SeverityRule rule = await _severityRuleRepository.GetByIdAsync(id, cancellationToken);

            if (rule is null)
            {
                return NotFound();
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = BuildAdministrationBreadcrumbs(Strings.DeleteButton);
            return View(rule);
        }

        /// <summary>Deletes a severity override rule.</summary>
        [HttpPost]
        [ActionName("DeleteSeverityRule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSeverityRuleConfirmed(long id, CancellationToken cancellationToken)
        {
            await _severityRuleRepository.DeleteAsync(id, cancellationToken);
            await RefreshSeverityRulesAsync(cancellationToken);
            TempData[Consts.SuccessMessageTempDataKey] = Strings.SeverityRuleDeletedMessage;
            return RedirectToAction(nameof(SeverityRules));
        }

        private async Task RefreshSeverityRulesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<SeverityRule> rules = await _severityRuleRepository.GetAllAsync(cancellationToken);
            _severityRuleProvider.Refresh(rules);
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

        private List<BreadcrumbItem> BuildSeverityRuleEditorBreadcrumbs(string activeTitle)
        {
            return new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.AdministrationTitle },
                new() { Title = Strings.SeverityRulesLabel, Url = Url.Action(nameof(SeverityRules)) },
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
