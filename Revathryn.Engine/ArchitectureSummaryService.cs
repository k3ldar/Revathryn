using System.Collections.Concurrent;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Provides the cached descriptive structure summary used to prime reviews.
    /// Summaries are cached per file fingerprint and regenerated when the file changes.
    /// </summary>
    public sealed class ArchitectureSummaryService : IArchitectureSummaryService
    {
        private readonly IRepoFileArchitectureRepository _architectureRepository;
        private readonly IArchitectureAnalysisFailureRepository _failureRepository;
        private readonly IArchitectureAnalysisEngine _architectureEngine;
        private readonly IOperationalDiagnostics _diagnostics;
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _fileLocks = new();

        /// <summary>Initialises a new service.</summary>
        public ArchitectureSummaryService(
            IRepoFileArchitectureRepository architectureRepository,
            IArchitectureAnalysisFailureRepository failureRepository,
            IArchitectureAnalysisEngine architectureEngine,
            IOperationalDiagnostics diagnostics)
        {
            _architectureRepository = architectureRepository;
            _failureRepository = failureRepository;
            _architectureEngine = architectureEngine;
            _diagnostics = diagnostics;
        }

        /// <inheritdoc />
        public async Task<string> GetOrCreateAsync(RepoFile file, string fileContent, string language, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);

            RepoFileArchitecture cached = await _architectureRepository.GetByRepoFileAsync(file.Id, cancellationToken).ConfigureAwait(false);

            if (IsCurrent(cached, file.Sha256))
            {
                return cached.Summary;
            }

            SemaphoreSlim gate = _fileLocks.GetOrAdd(file.Id, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                cached = await _architectureRepository.GetByRepoFileAsync(file.Id, cancellationToken).ConfigureAwait(false);

                if (IsCurrent(cached, file.Sha256))
                {
                    return cached.Summary;
                }

                return await GenerateAndCacheAsync(file, fileContent, language, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<string> GenerateAndCacheAsync(RepoFile file, string fileContent, string language, CancellationToken cancellationToken)
        {
            string prompt = DefaultReviewContext.ArchitecturePassPrompt;

            ArchitectureAnalysisRequest request = new()
            {
                RepositoryId = file.RepositoryId,
                RepoFileId = file.Id,
                Language = language ?? string.Empty,
                FileContent = fileContent ?? string.Empty,
                Prompt = prompt
            };

            ArchitectureAnalysisResponse response = await _architectureEngine.AnalyseStructureAsync(request, cancellationToken).ConfigureAwait(false);

            if (response is null || response.Skipped)
            {
                return string.Empty;
            }

            if (!response.Success)
            {
                await PersistFailureAsync(file, fileContent, language, prompt, response, cancellationToken).ConfigureAwait(false);
                return string.Empty;
            }

            RepoFileArchitecture architecture = new()
            {
                RepositoryId = file.RepositoryId,
                RepoFileId = file.Id,
                Sha256 = file.Sha256 ?? string.Empty,
                Summary = response.Summary ?? string.Empty,
                EngineName = response.EngineName ?? string.Empty,
                EngineModel = response.EngineModel ?? string.Empty,
                AnalysisVersion = Consts.ArchitectureAnalysisVersion,
                CreatedUtc = DateTime.UtcNow
            };

            await _architectureRepository.UpsertAsync(architecture, cancellationToken).ConfigureAwait(false);
            return architecture.Summary;
        }

        private async Task PersistFailureAsync(
            RepoFile file,
            string fileContent,
            string language,
            string prompt,
            ArchitectureAnalysisResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                ArchitectureAnalysisFailure failure = new()
                {
                    RepositoryId = file.RepositoryId,
                    RepoFileId = file.Id,
                    FilePath = file.RelativePath ?? string.Empty,
                    Language = language ?? string.Empty,
                    Sha256 = file.Sha256 ?? string.Empty,
                    SystemPrompt = prompt ?? string.Empty,
                    FileContent = fileContent ?? string.Empty,
                    Error = string.IsNullOrWhiteSpace(response?.Error) ? Strings.UnknownReviewError : response.Error,
                    RawResponse = response?.RawResponse ?? string.Empty,
                    EngineName = response?.EngineName ?? string.Empty,
                    EngineModel = response?.EngineModel ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow
                };

                await _failureRepository.AddAsync(failure, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _diagnostics.Log(
                    OperationalLogLevel.Error,
                    Consts.ArchitectureSummaryServiceComponent,
                    Consts.ArchitectureAnalysisOperation,
                    exception.Message,
                    exception);
            }
        }

        private static bool IsCurrent(RepoFileArchitecture cached, string sha256)
        {
            return cached is not null && string.Equals(cached.Sha256, sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
