using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>
    /// Coordinates a full repository scan: discovery, fingerprinting, file
    /// reconciliation, C# analysis, review scheduling and queue population.
    /// </summary>
    public sealed class RepositoryProcessor : IRepositoryProcessor
    {
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepoFileRepository _fileRepository;
        private readonly IRepositoryScanner _scanner;
        private readonly IReviewScheduler _scheduler;
        private readonly ICSharpAnalysisEngine _cSharpAnalysisEngine;
        private readonly IProjectDiscoveryService _projectDiscoveryService;
        private readonly IProjectRepository _projectRepository;
        private readonly IRepositoryScanRepository _scanRepository;
        private readonly RepositoryScanLockRegistry _lockRegistry;

        /// <summary>Initialises a new processor.</summary>
        public RepositoryProcessor(
            IRepositoryRepository repositoryRepository,
            IRepoFileRepository fileRepository,
            IRepositoryScanner scanner,
            IReviewScheduler scheduler,
            ICSharpAnalysisEngine cSharpAnalysisEngine,
            IProjectDiscoveryService projectDiscoveryService,
            IProjectRepository projectRepository,
            IRepositoryScanRepository scanRepository,
            RepositoryScanLockRegistry lockRegistry)
        {
            _repositoryRepository = repositoryRepository;
            _fileRepository = fileRepository;
            _scanner = scanner;
            _scheduler = scheduler;
            _cSharpAnalysisEngine = cSharpAnalysisEngine;
            _projectDiscoveryService = projectDiscoveryService;
            _projectRepository = projectRepository;
            _scanRepository = scanRepository;
            _lockRegistry = lockRegistry;
        }

        /// <summary>Scans and processes a single repository, returning a summary of the work performed.</summary>
        public async Task<RepositoryScanSummary> ProcessAsync(long repositoryId, CancellationToken cancellationToken, IProgress<RepositoryScanProgress> progress = null, bool forceFullScan = false)
        {
            Repository repository = await _repositoryRepository.GetByIdAsync(repositoryId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(string.Format(Strings.RepositoryNotFound, repositoryId));
            SemaphoreSlim scanLock = _lockRegistry.GetLock(repositoryId);
            await scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await ProcessCoreAsync(repository, cancellationToken, progress, forceFullScan).ConfigureAwait(false);
            }
            finally
            {
                scanLock.Release();
            }
        }

        /// <summary>Scans and processes every enabled repository, returning a summary per repository.</summary>
        public async Task<IReadOnlyList<RepositoryScanSummary>> ProcessAllEnabledAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
            List<RepositoryScanSummary> summaries = [];

            foreach (Repository repository in repositories)
            {
                summaries.Add(await ProcessAsync(repository.Id, cancellationToken).ConfigureAwait(false));
            }

            return summaries;
        }

        private async Task<RepositoryScanSummary> ProcessCoreAsync(Repository repository, CancellationToken cancellationToken, IProgress<RepositoryScanProgress> progress, bool forceFullScan)
        {
            RepositoryScanSummary summary = new()
            {
                RepositoryId = repository.Id,
                StartedUtc = DateTime.UtcNow
            };

            try
            {
                ReportProgress(progress, repository.Id, ScanPhase.Discovering, 0, 0, 0);

                IProgress<RepositoryScanProgress> scannerProgress = BuildScannerProgress(repository.Id, progress);
                IReadOnlyList<RepoFile> existingFiles = await _fileRepository.GetByRepositoryAsync(repository.Id, cancellationToken).ConfigureAwait(false);
                IReadOnlyDictionary<string, RepoFile> existingFileMap = BuildExistingFileMap(existingFiles);
                RepositoryScanResult scan = await _scanner.ScanAsync(repository.LocalPath, repository.ScanOptions, scannerProgress, cancellationToken, existingFileMap, forceFullScan).ConfigureAwait(false);

                ReportProgress(progress, repository.Id, ScanPhase.Reconciling, scan.DiscoveredFiles.Count, scan.DiscoveredFiles.Count, 0);

                IReadOnlyList<RepositoryProject> projects = await _projectDiscoveryService.DiscoverAsync(repository.LocalPath, cancellationToken).ConfigureAwait(false);
                await _projectRepository.ReconcileAsync(repository.Id, projects, cancellationToken).ConfigureAwait(false);
                AssociateProjectPaths(scan.DiscoveredFiles, projects);

                List<string> failedPaths = [];
                foreach (ScanFailure failure in scan.Failures)
                {
                    failedPaths.Add(failure.RelativePath);
                }

                RepositoryReconciliationResult reconciliation = await _fileRepository.ReconcileAsync(repository.Id, scan.DiscoveredFiles, failedPaths, cancellationToken).ConfigureAwait(false);

                summary.FilesDiscovered = scan.DiscoveredFiles.Count;
                summary.NewFiles = reconciliation.NewFiles;
                summary.ChangedFiles = reconciliation.ChangedFiles;
                summary.UnchangedFiles = reconciliation.UnchangedFiles;
                summary.RemovedFiles = reconciliation.RemovedFiles;
                summary.RenamedFiles = reconciliation.RenamedFiles;
                summary.Changes = reconciliation.Changes;

                ReportProgress(progress, repository.Id, ScanPhase.Analysing, summary.FilesDiscovered, summary.FilesDiscovered, summary.RemovedFiles);
                await AnalyseCSharpFilesAsync(repository, scan.DiscoveredFiles, summary, cancellationToken).ConfigureAwait(false);

                ReportProgress(progress, repository.Id, ScanPhase.Scheduling, summary.FilesDiscovered, summary.FilesDiscovered, summary.RemovedFiles);
                ReviewScheduleResult schedule = await _scheduler.ScheduleRepositoryAsync(repository.Id, cancellationToken).ConfigureAwait(false);
                summary.QueueEntriesAdded = schedule.Added;
                summary.QueueDuplicatesSkipped = schedule.DuplicatesSkipped;

                summary.CompletedUtc = DateTime.UtcNow;
                await RecordScanAsync(repository, summary, true, cancellationToken).ConfigureAwait(false);

                ReportProgress(progress, repository.Id, ScanPhase.Completed, summary.FilesDiscovered, summary.FilesDiscovered, summary.RemovedFiles);

                return summary;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                summary.CompletedUtc = DateTime.UtcNow;

                try
                {
                    await RecordScanAsync(repository, summary, false, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Recording a failed scan must not mask the original failure.
                }

                throw;
            }
        }

        private static IProgress<RepositoryScanProgress> BuildScannerProgress(long repositoryId, IProgress<RepositoryScanProgress> progress)
        {
            return progress is null ? null : new RepositoryProgressAdapter(repositoryId, progress);
        }

        private static IReadOnlyDictionary<string, RepoFile> BuildExistingFileMap(IReadOnlyList<RepoFile> existingFiles)
        {
            Dictionary<string, RepoFile> map = new(StringComparer.Ordinal);

            foreach (RepoFile file in existingFiles)
            {
                if (!file.IsRemoved)
                {
                    map[file.RelativePath] = file;
                }
            }

            return map;
        }

        private static void ReportProgress(IProgress<RepositoryScanProgress> progress, long repositoryId, ScanPhase phase, int discovered, int processed, int removed)
        {
            progress?.Report(new RepositoryScanProgress
            {
                RepositoryId = repositoryId,
                Phase = phase,
                FilesDiscovered = discovered,
                FilesProcessed = processed,
                FilesRemoved = removed,
                ReportedUtc = DateTime.UtcNow
            });
        }

        private static void AssociateProjectPaths(IReadOnlyList<RepoFile> discoveredFiles, IReadOnlyList<RepositoryProject> projects)
        {
            List<RepositoryProject> projectEntries = [.. projects
                .Where(project => project.Kind == ProjectKind.Project)
                .OrderByDescending(project => project.DirectoryPath.Length)];

            foreach (RepoFile file in discoveredFiles)
            {
                string fileDirectory = GetDirectory(file.RelativePath);
                RepositoryProject match = projectEntries.FirstOrDefault(project => IsWithinDirectory(fileDirectory, project.DirectoryPath));
                file.ProjectPath = match is null ? string.Empty : match.RelativePath;
            }
        }

        private static bool IsWithinDirectory(string fileDirectory, string projectDirectory)
        {
            if (projectDirectory.Length == 0)
            {
                return true;
            }

            if (!fileDirectory.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fileDirectory.Length == projectDirectory.Length || fileDirectory[projectDirectory.Length] == CharForwardSlash;
        }

        private static string GetDirectory(string relativePath)
        {
            int separator = relativePath.LastIndexOf(CharForwardSlash);

            if (separator < 0)
            {
                return string.Empty;
            }

            return relativePath[..separator];
        }

        private async Task RecordScanAsync(Repository repository, RepositoryScanSummary summary, bool succeeded, CancellationToken cancellationToken)
        {
            RepositoryScanRecord record = new()
            {
                RepositoryId = repository.Id,
                StartedUtc = summary.StartedUtc,
                CompletedUtc = summary.CompletedUtc,
                Succeeded = succeeded,
                FilesDiscovered = summary.FilesDiscovered,
                NewFiles = summary.NewFiles,
                ChangedFiles = summary.ChangedFiles,
                UnchangedFiles = summary.UnchangedFiles,
                RemovedFiles = summary.RemovedFiles,
                RenamedFiles = summary.RenamedFiles
            };

            await _scanRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);

            if (succeeded)
            {
                repository.LastSuccessfulScanDate = summary.CompletedUtc;
                await _repositoryRepository.UpdateAsync(repository, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AnalyseCSharpFilesAsync(
            Repository repository,
            IReadOnlyList<RepoFile> discoveredFiles,
            RepositoryScanSummary summary,
            CancellationToken cancellationToken)
        {
            foreach (RepoFile file in discoveredFiles)
            {
                if (!string.Equals(file.Extension, Consts.FileTypeCSharp, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string absolutePath = ResolveAbsolutePath(repository.LocalPath, file.RelativePath);

                try
                {
                    if (!_cSharpAnalysisEngine.RequiresAnalysis(absolutePath))
                    {
                        continue;
                    }

                    _ = await _cSharpAnalysisEngine.AnalyseSymbolsAsync(absolutePath, cancellationToken).ConfigureAwait(false);
                    summary.CSharpFilesAnalysed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    summary.CSharpAnalysisFailures++;
                }
            }
        }

        private static string ResolveAbsolutePath(string repositoryLocalPath, string relativePath)
        {
            string osRelativePath = relativePath.Replace(CharForwardSlash, Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repositoryLocalPath, osRelativePath));
        }

        private sealed class RepositoryProgressAdapter : IProgress<RepositoryScanProgress>
        {
            private readonly long _repositoryId;
            private readonly IProgress<RepositoryScanProgress> _inner;

            internal RepositoryProgressAdapter(long repositoryId, IProgress<RepositoryScanProgress> inner)
            {
                _repositoryId = repositoryId;
                _inner = inner;
            }

            public void Report(RepositoryScanProgress value)
            {
                value.RepositoryId = _repositoryId;
                _inner.Report(value);
            }
        }
    }
}