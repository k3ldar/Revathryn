using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Models.Api;
using Revathryn.Models.IssueValidations;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>
    /// JSON API exposing everything Revathryn holds about a single repository file, keyed by
    /// repository name and file path.
    /// </summary>
    [ApiController]
    [Route(Consts.FileDetailsApiRoute)]
    public sealed class FileDetailsController : ControllerBase
    {
        private readonly IRepositoryService _repositoryService;
        private readonly IRepositoryFileService _repositoryFileService;
        private readonly IFindingRepository _findingRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IIssueValidationService _validationService;
        private readonly IRepoFileArchitectureRepository _architectureRepository;
        private readonly IRepoFileAnalysisRepository _analysisRepository;
        private readonly IReviewScheduler _reviewScheduler;
        private readonly ISymbolIndexStore _symbolIndexStore;

        /// <summary>Initialises a new controller.</summary>
        public FileDetailsController(
            IRepositoryService repositoryService,
            IRepositoryFileService repositoryFileService,
            IFindingRepository findingRepository,
            IReviewTypeRepository reviewTypeRepository,
            IReviewContextRepository reviewContextRepository,
            IIssueValidationService validationService,
            IRepoFileArchitectureRepository architectureRepository,
            IRepoFileAnalysisRepository analysisRepository,
            IReviewScheduler reviewScheduler,
            ISymbolIndexStore symbolIndexStore)
        {
            _repositoryService = repositoryService;
            _repositoryFileService = repositoryFileService;
            _findingRepository = findingRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _reviewContextRepository = reviewContextRepository;
            _validationService = validationService;
            _architectureRepository = architectureRepository;
            _analysisRepository = analysisRepository;
            _reviewScheduler = reviewScheduler;
            _symbolIndexStore = symbolIndexStore;
        }

        /// <summary>
        /// Returns all information held about a file, including the cached architecture overview,
        /// review coverage, issues with their latest validations, and code-analysis symbols.
        /// </summary>
        /// <param name="repo">Repository name.</param>
        /// <param name="file">File path relative to the repository root, or file name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet]
        public async Task<IActionResult> Get(string repo, string file, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                return BadRequest(new { message = Strings.RepositoryRequiredMessage });
            }

            if (string.IsNullOrWhiteSpace(file))
            {
                return BadRequest(new { message = Strings.PathRequired });
            }

            Repository repository = await ResolveRepositoryAsync(repo, cancellationToken);

            if (repository is null)
            {
                return NotFound(new { message = string.Format(Strings.RepositoryNotFound, repo) });
            }

            RepoFile repoFile = await ResolveFileAsync(repository.Id, file, cancellationToken);

            if (repoFile is null)
            {
                return NotFound(new { message = string.Format(Strings.RepoFileNotFound, file) });
            }

            FileDetailsResponse response = await BuildResponseAsync(repository, repoFile, cancellationToken);
            return Ok(response);
        }

        private async Task<Repository> ResolveRepositoryAsync(string name, CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);
            return repositories.FirstOrDefault(repository => string.Equals(repository.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<RepoFile> ResolveFileAsync(long repositoryId, string file, CancellationToken cancellationToken)
        {
            IReadOnlyList<RepoFile> files = await _repositoryFileService.GetFilesAsync(repositoryId, cancellationToken);
            string normalized = file.Replace(Consts.CharBackSlash, Consts.CharForwardSlash);

            return files.FirstOrDefault(candidate =>
                !candidate.IsRemoved &&
                (string.Equals(candidate.RelativePath, normalized, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.FileName, file, StringComparison.OrdinalIgnoreCase)));
        }

        private async Task<FileDetailsResponse> BuildResponseAsync(Repository repository, RepoFile file, CancellationToken cancellationToken)
        {
            RepoFileArchitecture architecture = await _architectureRepository.GetByRepoFileAsync(file.Id, cancellationToken);
            IReadOnlyList<Finding> findings = await _findingRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<RepoFileAnalysis> analyses = await _analysisRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
            IReadOnlyList<IndexedSymbol> symbols = await LoadSymbolsAsync(repository, file, cancellationToken);
            IReadOnlyList<FileReviewStatus> reviews = await BuildReviewStatusesAsync(file, reviewTypes, analyses, cancellationToken);
            IReadOnlyList<FileIssueDetails> issues = await BuildIssuesAsync(file, findings, reviewTypes, cancellationToken);

            return new FileDetailsResponse
            {
                Repository = repository,
                File = file,
                Architecture = architecture,
                Reviews = reviews,
                Issues = issues,
                Symbols = symbols,
                ExportedUtc = DateTime.UtcNow
            };
        }

        private async Task<IReadOnlyList<FileReviewStatus>> BuildReviewStatusesAsync(
            RepoFile file,
            IReadOnlyList<ReviewType> reviewTypes,
            IReadOnlyList<RepoFileAnalysis> analyses,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewType> applicable = await _reviewScheduler.GetApplicableReviewTypesAsync(file, cancellationToken);
            List<FileReviewStatus> statuses = [];

            foreach (ReviewType reviewType in applicable)
            {
                bool isCurrent = await _reviewScheduler.IsCurrentAsync(file, reviewType, cancellationToken);
                RepoFileAnalysis analysis = isCurrent
                    ? analyses.FirstOrDefault(item => item.RepoFileId == file.Id && item.ReviewTypeId == reviewType.Id)
                    : null;

                statuses.Add(new FileReviewStatus
                {
                    ReviewTypeId = reviewType.Id,
                    ReviewTypeName = reviewType.Name,
                    Status = isCurrent ? Consts.ReviewStatusCompleted : Consts.ReviewStatusOutstanding,
                    LastAnalysedUtc = analysis?.LastAnalysedDate,
                    AnalysisVersion = analysis?.AnalysisVersion ?? string.Empty,
                    ReviewDefinitionVersion = analysis?.ReviewDefinitionVersion ?? string.Empty,
                    Branch = analysis?.Branch ?? string.Empty
                });
            }

            return statuses;
        }

        private async Task<IReadOnlyList<FileIssueDetails>> BuildIssuesAsync(
            RepoFile file,
            IReadOnlyList<Finding> findings,
            IReadOnlyList<ReviewType> reviewTypes,
            CancellationToken cancellationToken)
        {
            List<Finding> fileFindings = [.. findings
                .Where(finding => finding.RepoFileId == file.Id)
                .OrderByDescending(finding => finding.Id)];

            Dictionary<long, string> reviewTypeNames = reviewTypes.ToDictionary(reviewType => reviewType.Id, reviewType => reviewType.Name);

            IReadOnlyDictionary<long, IssueValidation> validations = await _validationService.GetLatestCompletedAsync(
                [.. fileFindings.Select(finding => finding.Id)],
                cancellationToken);

            List<FileIssueDetails> issues = [];

            foreach (Finding finding in fileFindings)
            {
                reviewTypeNames.TryGetValue(finding.ReviewTypeId.GetValueOrDefault(), out string reviewTypeName);
                validations.TryGetValue(finding.Id, out IssueValidation validation);

                FileReviewContextSummary context = null;

                if (finding.ReviewContextId.HasValue)
                {
                    AIReviewContext stored = await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken);

                    if (stored is not null)
                    {
                        context = BuildContextSummary(stored);
                    }
                }

                issues.Add(new FileIssueDetails
                {
                    Finding = finding,
                    ReviewTypeName = reviewTypeName ?? string.Empty,
                    FileRelativePath = file.RelativePath,
                    Context = context,
                    LatestValidation = validation is null ? null : IssueValidationDto.FromModel(validation, isLatest: true)
                });
            }

            return issues;
        }

        private static FileReviewContextSummary BuildContextSummary(AIReviewContext context)
        {
            return new FileReviewContextSummary
            {
                Id = context.Id,
                EngineName = context.EngineName,
                EngineProvider = context.EngineProvider,
                EngineModel = context.EngineModel,
                Language = context.Language,
                ReviewDefinitionVersion = context.ReviewDefinitionVersion,
                RequestedContextKeys = context.RequestedContextKeys ?? [],
                Branch = context.Branch,
                CreatedUtc = context.CreatedUtc
            };
        }

        private async Task<IReadOnlyList<IndexedSymbol>> LoadSymbolsAsync(Repository repository, RepoFile file, CancellationToken cancellationToken)
        {
            string sourcePath = ResolveSourcePath(repository.LocalPath, file.RelativePath);
            return await _symbolIndexStore.GetSymbolsForFileAsync(repository.Name, sourcePath, cancellationToken);
        }

        private static string ResolveSourcePath(string repositoryLocalPath, string relativePath)
        {
            string osRelativePath = relativePath.Replace(Consts.CharForwardSlash, Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repositoryLocalPath, osRelativePath));
        }
    }
}
