using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Models;
using Revathryn.Models.Issues;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>Read-only issue browsing and inspection.</summary>
    public sealed class IssuesController : Controller
    {
        private readonly IRepositoryService _repositoryService;
        private readonly IRepositoryFileService _repositoryFileService;
        private readonly IFindingRepository _findingRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IssueViewingOptions _options;

        /// <summary>Initialises a new issues controller.</summary>
        public IssuesController(
            IRepositoryService repositoryService,
            IRepositoryFileService repositoryFileService,
            IFindingRepository findingRepository,
            IReviewTypeRepository reviewTypeRepository,
            IReviewContextRepository reviewContextRepository,
            IOptions<IssueViewingOptions> options)
        {
            _repositoryService = repositoryService;
            _repositoryFileService = repositoryFileService;
            _findingRepository = findingRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _reviewContextRepository = reviewContextRepository;
            _options = options.Value;
        }

        /// <summary>Renders repository selection for issue browsing.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);
            IReadOnlyList<RepositoryIssueCount> counts = await _findingRepository.CountByRepositoryAsync(cancellationToken);

            Dictionary<long, int> countsByRepository = [];
            foreach (RepositoryIssueCount count in counts)
            {
                countsByRepository[count.RepositoryId] = count.Count;
            }

            List<IssueRepositoryListItemViewModel> items = [];
            foreach (Repository repository in repositories.OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase))
            {
                countsByRepository.TryGetValue(repository.Id, out int issueCount);

                items.Add(new IssueRepositoryListItemViewModel
                {
                    Repository = repository,
                    IssueCount = issueCount
                });
            }

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.BrowseIssuesTitle, IsActive = true }
            };

            return View(new IssueRepositoryListViewModel { Repositories = items });
        }

        /// <summary>Renders the paginated issue listing for a repository.</summary>
        public async Task<IActionResult> List(
            long id,
            int? page,
            int? pageSize,
            FindingSeverity? severity,
            long? reviewTypeId,
            string search,
            FindingSortField? sort,
            bool? descending,
            CancellationToken cancellationToken)
        {
            Repository repository = await _repositoryService.GetAsync(id, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            FindingQuery query = BuildQuery(id, page, pageSize, severity, reviewTypeId, search, sort, descending);
            FindingQueryResult result = await _findingRepository.QueryAsync(query, cancellationToken);
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(id, cancellationToken);
            IReadOnlyList<RepoFile> files = await _repositoryFileService.GetFilesAsync(id, cancellationToken);

            Dictionary<long, string> reviewTypeNames = [];
            foreach (ReviewType reviewType in reviewTypes)
            {
                reviewTypeNames[reviewType.Id] = reviewType.Name;
            }

            Dictionary<long, string> filePaths = [];
            foreach (RepoFile file in files)
            {
                filePaths[file.Id] = file.RelativePath;
            }

            List<IssueListItemViewModel> items = [];
            foreach (Finding finding in result.Items)
            {
                reviewTypeNames.TryGetValue(finding.ReviewTypeId.GetValueOrDefault(), out string reviewTypeName);
                filePaths.TryGetValue(finding.RepoFileId.GetValueOrDefault(), out string filePath);

                items.Add(new IssueListItemViewModel
                {
                    Finding = finding,
                    ReviewTypeName = reviewTypeName ?? string.Empty,
                    FileRelativePath = filePath ?? string.Empty
                });
            }

            IssueListViewModel model = new()
            {
                Repository = repository,
                Items = items,
                ReviewTypes = reviewTypes,
                TotalCount = result.TotalCount,
                PageIndex = query.PageIndex + 1,
                PageSize = query.PageSize,
                TotalPages = CalculateTotalPages(result.TotalCount, query.PageSize),
                PageSizeOptions = _options.PageSizeOptions,
                Severity = query.Severity,
                ReviewTypeId = query.ReviewTypeId,
                Search = query.SearchText,
                SortField = query.SortField,
                SortDescending = query.SortDescending
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.BrowseIssuesTitle, Url = Url.Action(nameof(Index)) },
                new() { Title = repository.Name, IsActive = true }
            };

            return View(model);
        }

        /// <summary>Renders complete details for a single issue, including original AI review context.</summary>
        public async Task<IActionResult> Details(
            long id,
            long? repositoryId,
            int? page,
            int? pageSize,
            FindingSeverity? severity,
            long? reviewTypeId,
            string search,
            FindingSortField? sort,
            bool? descending,
            CancellationToken cancellationToken)
        {
            Finding finding = await _findingRepository.GetByIdAsync(id, cancellationToken);

            if (finding is null)
            {
                return NotFound();
            }

            Repository repository = await _repositoryService.GetAsync(finding.RepositoryId, cancellationToken);

            if (repository is null)
            {
                return NotFound();
            }

            string reviewTypeName = await ResolveReviewTypeNameAsync(finding.ReviewTypeId, cancellationToken);
            string filePath = await ResolveFilePathAsync(finding.RepoFileId, cancellationToken);
            AIReviewContext context = finding.ReviewContextId.HasValue
                ? await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken)
                : null;

            long browseRepositoryId = repositoryId ?? finding.RepositoryId;
            FindingQuery query = BuildQuery(browseRepositoryId, page, pageSize, severity, reviewTypeId, search, sort, descending);
            FindingNavigation navigation = await _findingRepository.GetNavigationAsync(id, query, cancellationToken);

            IssueDetailViewModel model = new()
            {
                Finding = finding,
                Repository = repository,
                ReviewTypeName = reviewTypeName,
                FileRelativePath = filePath,
                Context = context,
                PreviousFindingId = navigation.PreviousId,
                NextFindingId = navigation.NextId,
                ListState = new IssueListQueryState
                {
                    RepositoryId = browseRepositoryId,
                    Page = Math.Max(page ?? 1, 1),
                    PageSize = query.PageSize,
                    Severity = query.Severity,
                    ReviewTypeId = query.ReviewTypeId,
                    Search = query.SearchText,
                    SortField = query.SortField,
                    SortDescending = query.SortDescending
                }
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.BrowseIssuesTitle, Url = Url.Action(nameof(Index)) },
                new() { Title = repository.Name, Url = Url.Action(nameof(List), BuildListRouteValues(browseRepositoryId, query, page)) },
                new() { Title = string.IsNullOrWhiteSpace(finding.Title) ? finding.Id.ToString() : finding.Title, IsActive = true }
            };

            return View(model);
        }

        private FindingQuery BuildQuery(
            long repositoryId,
            int? page,
            int? pageSize,
            FindingSeverity? severity,
            long? reviewTypeId,
            string search,
            FindingSortField? sort,
            bool? descending)
        {
            int resolvedPageSize = ResolvePageSize(pageSize);
            int resolvedPage = Math.Max(page ?? 1, 1);

            return new FindingQuery
            {
                RepositoryId = repositoryId,
                Severity = severity,
                ReviewTypeId = reviewTypeId,
                SearchText = search ?? string.Empty,
                SortField = sort ?? FindingSortField.Severity,
                SortDescending = descending ?? true,
                PageIndex = resolvedPage - 1,
                PageSize = resolvedPageSize
            };
        }

        private int ResolvePageSize(int? pageSize)
        {
            if (pageSize.HasValue && _options.PageSizeOptions.Contains(pageSize.Value))
            {
                return pageSize.Value;
            }

            return _options.DefaultPageSize;
        }

        private static int CalculateTotalPages(int totalCount, int pageSize)
        {
            if (totalCount == 0 || pageSize <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(totalCount / (double)pageSize);
        }

        private async Task<string> ResolveReviewTypeNameAsync(long? reviewTypeId, CancellationToken cancellationToken)
        {
            if (!reviewTypeId.HasValue)
            {
                return string.Empty;
            }

            ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(reviewTypeId.Value, cancellationToken);
            return reviewType?.Name ?? string.Empty;
        }

        private async Task<string> ResolveFilePathAsync(long? repoFileId, CancellationToken cancellationToken)
        {
            if (!repoFileId.HasValue)
            {
                return string.Empty;
            }

            RepoFile file = await _repositoryFileService.GetFileAsync(repoFileId.Value, cancellationToken);
            return file?.RelativePath ?? string.Empty;
        }

        private static object BuildListRouteValues(long repositoryId, FindingQuery query, int? page)
        {
            return new
            {
                id = repositoryId,
                page = Math.Max(page ?? 1, 1),
                pageSize = query.PageSize,
                severity = query.Severity.HasValue ? (int)query.Severity.Value : (int?)null,
                reviewTypeId = query.ReviewTypeId,
                search = query.SearchText,
                sort = (int)query.SortField,
                descending = query.SortDescending
            };
        }
    }
}
