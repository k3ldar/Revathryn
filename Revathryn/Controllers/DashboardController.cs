using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Models;
using Revathryn.Models.Dashboard;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>Administration dashboard controller.</summary>
    public class DashboardController : Controller
    {
        private const int PercentageScale = 100;

        private readonly IDashboardQueryService _dashboardQueryService;
        private readonly IRepositoryService _repositoryService;
        private readonly IRepositoryFileService _repositoryFileService;
        private readonly IFindingRepository _findingRepository;
        private readonly IRepoFileAnalysisRepository _analysisRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IReviewScheduler _reviewScheduler;

        /// <summary>Initialises a new controller.</summary>
        public DashboardController(
            IDashboardQueryService dashboardQueryService,
            IRepositoryService repositoryService,
            IRepositoryFileService repositoryFileService,
            IFindingRepository findingRepository,
            IRepoFileAnalysisRepository analysisRepository,
            IReviewTypeRepository reviewTypeRepository,
            IProjectRepository projectRepository,
            IReviewScheduler reviewScheduler)
        {
            _dashboardQueryService = dashboardQueryService;
            _repositoryService = repositoryService;
            _repositoryFileService = repositoryFileService;
            _findingRepository = findingRepository;
            _analysisRepository = analysisRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _projectRepository = projectRepository;
            _reviewScheduler = reviewScheduler;
        }

        /// <summary>Renders the operational administration dashboard.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            DashboardSnapshot snapshot = await _dashboardQueryService.GetSnapshotAsync(cancellationToken);
            return View(snapshot);
        }

        /// <summary>Returns the current dashboard content for live polling refresh.</summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Data(CancellationToken cancellationToken)
        {
            DashboardSnapshot snapshot = await _dashboardQueryService.GetSnapshotAsync(cancellationToken);
            return PartialView(Consts.DashboardContentViewName, snapshot);
        }

        /// <summary>Renders the generic error view used by the exception handler.</summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            ErrorViewModel model = new()
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };

            return View(model);
        }

        /// <summary>Renders repository and module-level review lifecycle visibility.</summary>
        public async Task<IActionResult> Repository(long id, CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            RepositoryDetailsViewModel model = await BuildRepositoryDetailsAsync(repository, cancellationToken);
            return View(model);
        }

        /// <summary>Renders file-level review lifecycle visibility.</summary>
        public async Task<IActionResult> File(long id, CancellationToken cancellationToken)
        {
            RepoFile file = await _repositoryFileService.GetFileAsync(id, cancellationToken);

            if (file is null)
            {
                return NotFound();
            }

            Repository repository = await _repositoryService.GetAsync(file.RepositoryId, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            FileDetailsViewModel model = await BuildFileDetailsAsync(repository, file, cancellationToken);
            return View(model);
        }

        private async Task<RepositoryDetailsViewModel> BuildRepositoryDetailsAsync(Repository repository, CancellationToken cancellationToken)
        {
            IReadOnlyList<RepoFile> files = await _repositoryFileService.GetFilesAsync(repository.Id, cancellationToken);
            IReadOnlyList<Finding> findings = await _findingRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<RepositoryProject> projects = await _projectRepository.GetByRepositoryAsync(repository.Id, cancellationToken);

            IReadOnlyDictionary<long, ReviewType> reviewTypesById = reviewTypes.ToDictionary(reviewType => reviewType.Id);

            FileCoverageResult coverage = await BuildFileCoverageAsync(files, cancellationToken);

            IReadOnlyDictionary<long, List<Finding>> findingsByFile = findings
                .Where(finding => finding.Category != FindingCategory.OperationalFailure && finding.RepoFileId is not null)
                .GroupBy(finding => finding.RepoFileId.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (FileSummaryViewModel summary in coverage.Summaries)
            {
                if (findingsByFile.TryGetValue(summary.File.Id, out List<Finding> fileFindings))
                {
                    summary.FindingsCount = fileFindings.Count;
                }
            }

            List<ModuleViewModel> modules = [];
            HashSet<long> assignedFileIds = [];

            foreach (RepositoryProject project in projects.Where(project => project.Kind == ProjectKind.Project))
            {
                List<FileSummaryViewModel> moduleFiles = [.. coverage.Summaries
                    .Where(summary => string.Equals(summary.File.ProjectPath, project.RelativePath, StringComparison.OrdinalIgnoreCase))];

                foreach (FileSummaryViewModel moduleFile in moduleFiles)
                {
                    assignedFileIds.Add(moduleFile.File.Id);
                }

                modules.Add(BuildModule(project, moduleFiles, findingsByFile, reviewTypesById));
            }

            List<FileSummaryViewModel> unassignedFiles = [.. coverage.Summaries
                .Where(summary => !assignedFileIds.Contains(summary.File.Id))];

            int findingsCount = findings.Count(finding => finding.Category != FindingCategory.OperationalFailure);
            int operationalFailuresCount = findings.Count(finding => finding.Category == FindingCategory.OperationalFailure);

            return new RepositoryDetailsViewModel
            {
                Repository = repository,
                FilesDiscovered = files.Count,
                FilesRequiringReview = coverage.FilesRequiringReview,
                OutstandingReviews = coverage.OutstandingReviews,
                FindingsCount = findingsCount,
                OperationalFailuresCount = operationalFailuresCount,
                ReviewStatus = ResolveStatus(files.Count, coverage.OutstandingReviews, findingsCount),
                Modules = modules,
                UnassignedFiles = unassignedFiles
            };
        }

        private async Task<FileDetailsViewModel> BuildFileDetailsAsync(Repository repository, RepoFile file, CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<RepoFileAnalysis> analyses = await _analysisRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<Finding> findings = await _findingRepository.GetByRepositoryAsync(repository.Id, cancellationToken);

            IReadOnlyDictionary<long, ReviewType> reviewTypesById = reviewTypes.ToDictionary(reviewType => reviewType.Id);
            IReadOnlyList<ReviewType> applicable = await _reviewScheduler.GetApplicableReviewTypesAsync(file, cancellationToken);

            List<ReviewOutcomeViewModel> completed = [];
            List<ReviewType> outstanding = [];

            foreach (ReviewType reviewType in applicable)
            {
                if (await _reviewScheduler.IsCurrentAsync(file, reviewType, cancellationToken))
                {
                    RepoFileAnalysis analysis = analyses.FirstOrDefault(item => item.RepoFileId == file.Id && item.ReviewTypeId == reviewType.Id);

                    if (analysis is not null)
                    {
                        completed.Add(new ReviewOutcomeViewModel
                        {
                            Analysis = analysis,
                            ReviewTypeName = reviewType.Name
                        });
                    }
                }
                else
                {
                    outstanding.Add(reviewType);
                }
            }

            List<FindingViewModel> fileFindings = [.. findings
                .Where(finding => finding.RepoFileId == file.Id)
                .Select(finding => new FindingViewModel
                {
                    Finding = finding,
                    ReviewTypeName = GetReviewTypeName(finding.ReviewTypeId, reviewTypesById),
                    FileRelativePath = file.RelativePath
                })];

            return new FileDetailsViewModel
            {
                Repository = repository,
                File = file,
                ApplicableReviewTypes = applicable,
                CompletedReviews = completed,
                OutstandingReviewTypes = outstanding,
                Findings = fileFindings
            };
        }

        private async Task<FileCoverageResult> BuildFileCoverageAsync(IReadOnlyList<RepoFile> files, CancellationToken cancellationToken)
        {
            FileCoverageResult result = new();

            foreach (RepoFile file in files)
            {
                IReadOnlyList<ReviewType> applicable = await _reviewScheduler.GetApplicableReviewTypesAsync(file, cancellationToken);

                int completed = 0;

                foreach (ReviewType reviewType in applicable)
                {
                    if (await _reviewScheduler.IsCurrentAsync(file, reviewType, cancellationToken))
                    {
                        completed++;
                    }
                }

                int outstanding = applicable.Count - completed;

                result.Summaries.Add(new FileSummaryViewModel
                {
                    File = file,
                    CompletedReviews = completed,
                    OutstandingReviews = outstanding
                });

                if (applicable.Count > 0)
                {
                    result.FilesRequiringReview++;
                }

                result.CompletedReviews += completed;
                result.OutstandingReviews += outstanding;
            }

            return result;
        }

        private static ModuleViewModel BuildModule(
            RepositoryProject project,
            IReadOnlyList<FileSummaryViewModel> files,
            IReadOnlyDictionary<long, List<Finding>> findingsByFile,
            IReadOnlyDictionary<long, ReviewType> reviewTypesById)
        {
            int required = files.Sum(file => file.CompletedReviews + file.OutstandingReviews);
            int completed = files.Sum(file => file.CompletedReviews);

            List<Finding> moduleFindings = [];

            foreach (FileSummaryViewModel file in files)
            {
                if (findingsByFile.TryGetValue(file.File.Id, out List<Finding> fileFindings))
                {
                    moduleFindings.AddRange(fileFindings);
                }
            }

            List<FindingGroupViewModel> groups = [.. moduleFindings
                .GroupBy(finding => new { finding.Severity, finding.ReviewTypeId })
                .OrderBy(group => group.Key.ReviewTypeId)
                .ThenBy(group => group.Key.Severity)
                .Select(group => new FindingGroupViewModel
                {
                    Severity = group.Key.Severity,
                    ReviewTypeName = GetReviewTypeName(group.Key.ReviewTypeId, reviewTypesById),
                    Findings = [.. group]
                })];

            string name = string.IsNullOrEmpty(project.Name)
                ? Path.GetFileName(project.RelativePath)
                : project.Name;

            return new ModuleViewModel
            {
                Name = name,
                RelativePath = project.RelativePath,
                Files = files,
                RequiredReviews = required,
                CompletedReviews = completed,
                OutstandingReviews = required - completed,
                ReviewCoveragePercent = required == 0 ? 0 : completed * PercentageScale / required,
                FindingGroups = groups
            };
        }

        private static string GetReviewTypeName(long? reviewTypeId, IReadOnlyDictionary<long, ReviewType> reviewTypesById)
        {
            if (reviewTypeId is null)
            {
                return string.Empty;
            }

            return reviewTypesById.TryGetValue(reviewTypeId.Value, out ReviewType reviewType) ? reviewType.Name : string.Empty;
        }

        private static string ResolveStatus(int filesDiscovered, int outstandingReviews, int findingsCount)
        {
            if (filesDiscovered == 0)
            {
                return Strings.RepositoryReviewStatusNotScanned;
            }

            if (outstandingReviews > 0)
            {
                return Strings.RepositoryReviewStatusInProgress;
            }

            if (findingsCount > 0)
            {
                return Strings.RepositoryReviewStatusIssuesFound;
            }

            return Strings.RepositoryReviewStatusComplete;
        }

        private sealed class FileCoverageResult
        {
            public List<FileSummaryViewModel> Summaries { get; set; } = [];

            public int FilesRequiringReview { get; set; }

            public int CompletedReviews { get; set; }

            public int OutstandingReviews { get; set; }
        }
    }
}
