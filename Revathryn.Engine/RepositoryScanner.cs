using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Discovers files beneath a local repository folder.</summary>
    public sealed class RepositoryScanner : IRepositoryScanner
    {
        private static readonly string[] _defaultExcludedDirectories =
        [
            FilePathGit, FilePathVSInternal, FilePathBin, FilePathObj, FilePathNodeModules, FilePathPackages, FilePathBuild, FilePathDist
        ];

        private static readonly string[] _defaultIncludedExtensions =
        [
            FileTypeCSharp, FileTypeJson, FileTypeXml, FileTypeConfig, FileTypeCSharpProj, FileTypeProps, FileTypeTargets, FileTypeMarkdown, FileTypeYml, FileTypeYaml
        ];

        private readonly IFileHasher _fileHasher;

        /// <summary>Initialises a new scanner.</summary>
        /// <param name="fileHasher">Fingerprinting service; defaults to the central SHA-256 hasher when not supplied.</param>
        public RepositoryScanner(IFileHasher fileHasher = null)
        {
            _fileHasher = fileHasher ?? new FileHasher();
        }

        /// <summary>Scans a local folder and returns the files discovered beneath it.</summary>
        public async Task<RepositoryScanResult> ScanAsync(
            string localPath,
            RepositoryScanOptions options = null,
            IProgress<RepositoryScanProgress> progress = null,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, RepoFile> existingFiles = null,
            bool forceFullScan = false)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException(Strings.LocalPathRequired, nameof(localPath));
            }

            if (!Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException(string.Format(Strings.RepositoryFolderNotFound, localPath));
            }

            string fullRoot = Path.GetFullPath(localPath);
            RepositoryScanOptions effectiveOptions = options ?? new RepositoryScanOptions();
            IReadOnlyCollection<string> excludedDirectories = GetExcludedDirectories(effectiveOptions);
            IReadOnlyCollection<string> includedExtensions = GetIncludedExtensions(effectiveOptions);
            GitIgnoreMatcher gitIgnore = effectiveOptions.RespectGitIgnore ? new GitIgnoreMatcher(fullRoot) : null;

            List<RepoFile> files = [];
            List<ScanFailure> failures = [];
            ScanState state = new();

            await ScanDirectoryAsync(
                fullRoot,
                string.Empty,
                effectiveOptions,
                excludedDirectories,
                includedExtensions,
                gitIgnore,
                files,
                failures,
                progress,
                state,
                existingFiles,
                forceFullScan,
                cancellationToken).ConfigureAwait(false);

            return new RepositoryScanResult
            {
                DiscoveredFiles = files,
                Failures = failures
            };
        }

        private async Task ScanDirectoryAsync(
            string fullRoot,
            string relativeDirectory,
            RepositoryScanOptions options,
            IReadOnlyCollection<string> excludedDirectories,
            IReadOnlyCollection<string> includedExtensions,
            GitIgnoreMatcher gitIgnore,
            List<RepoFile> files,
            List<ScanFailure> failures,
            IProgress<RepositoryScanProgress> progress,
            ScanState state,
            IReadOnlyDictionary<string, RepoFile> existingFiles,
            bool forceFullScan,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string currentDirectory = relativeDirectory.Length == 0
                ? fullRoot
                : Path.Combine(fullRoot, relativeDirectory.Replace(CharForwardSlash, Path.DirectorySeparatorChar));

            IEnumerable<string> entries;

            try
            {
                entries = Directory.EnumerateFileSystemEntries(currentDirectory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return;
            }

            foreach (string entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;

                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                string relativePath = NormaliseRelativePath(Path.GetRelativePath(fullRoot, entry));
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;

                if (isDirectory)
                {
                    if (IsExcludedDirectory(relativePath, excludedDirectories))
                    {
                        continue;
                    }

                    if (gitIgnore is not null && gitIgnore.IsIgnored(relativePath, true))
                    {
                        continue;
                    }

                    await ScanDirectoryAsync(
                        fullRoot,
                        relativePath,
                        options,
                        excludedDirectories,
                        includedExtensions,
                        gitIgnore,
                        files,
                        failures,
                        progress,
                        state,
                        existingFiles,
                        forceFullScan,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                state.FilesDiscovered++;
                ReportProgress(progress, state);

                if (IsExcludedDirectory(relativePath, excludedDirectories))
                {
                    continue;
                }

                if (gitIgnore is not null && gitIgnore.IsIgnored(relativePath, false))
                {
                    continue;
                }

                if (!HasIncludedExtension(entry, includedExtensions))
                {
                    continue;
                }

                if (!MatchesPatterns(relativePath, options))
                {
                    continue;
                }

                try
                {
                    if (IsBinaryFile(entry))
                    {
                        continue;
                    }

                    files.Add(await BuildRepoFileAsync(fullRoot, entry, relativePath, existingFiles, forceFullScan, cancellationToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    failures.Add(new ScanFailure
                    {
                        RelativePath = relativePath,
                        Error = ex.Message
                    });
                }
                finally
                {
                    state.FilesProcessed++;
                    ReportProgress(progress, state);
                }
            }
        }

        private static IReadOnlyCollection<string> GetExcludedDirectories(RepositoryScanOptions options)
        {
            if (options.ExcludedDirectoryNames is null || options.ExcludedDirectoryNames.Count == 0)
            {
                return _defaultExcludedDirectories;
            }

            List<string> merged = new(_defaultExcludedDirectories);

            foreach (string directory in options.ExcludedDirectoryNames)
            {
                if (!string.IsNullOrWhiteSpace(directory) && !merged.Contains(directory, StringComparer.OrdinalIgnoreCase))
                {
                    merged.Add(directory);
                }
            }

            return merged;
        }

        private static IReadOnlyCollection<string> GetIncludedExtensions(RepositoryScanOptions options)
        {
            if (options.IncludedExtensions is not null && options.IncludedExtensions.Count > 0)
            {
                return options.IncludedExtensions;
            }

            return _defaultIncludedExtensions;
        }

        private async Task<RepoFile> BuildRepoFileAsync(
            string rootPath,
            string filePath,
            string relativePath,
            IReadOnlyDictionary<string, RepoFile> existingFiles,
            bool forceFullScan,
            CancellationToken cancellationToken)
        {
            FileInfo info = new(filePath);

            string sha256 = GetReusableFingerprint(relativePath, info, existingFiles, forceFullScan);

            if (sha256 is null)
            {
                sha256 = await _fileHasher.ComputeHashAsync(filePath, cancellationToken).ConfigureAwait(false);
            }

            return new RepoFile
            {
                RelativePath = relativePath,
                FileName = info.Name,
                Extension = NormaliseExtension(info.Extension),
                LastModifiedDate = info.LastWriteTimeUtc,
                Sha256 = sha256,
                FileSize = info.Length,
                IsExcluded = false,
                LastDiscoveredDate = DateTime.UtcNow
            };
        }

        private static string GetReusableFingerprint(
            string relativePath,
            FileInfo info,
            IReadOnlyDictionary<string, RepoFile> existingFiles,
            bool forceFullScan)
        {
            if (forceFullScan || existingFiles is null)
            {
                return null;
            }

            if (!existingFiles.TryGetValue(relativePath, out RepoFile previous))
            {
                return null;
            }

            if (previous.Sha256.Length == 0 || previous.FileSize != info.Length || previous.LastModifiedDate != info.LastWriteTimeUtc)
            {
                return null;
            }

            return previous.Sha256;
        }

        private static bool IsBinaryFile(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] buffer = new byte[BinaryDetectionBufferSize];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            for (int index = 0; index < bytesRead; index++)
            {
                if (buffer[index] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesPatterns(string relativePath, RepositoryScanOptions options)
        {
            if (options.IncludedFilePatterns is not null && options.IncludedFilePatterns.Count > 0)
            {
                bool matched = false;

                foreach (string pattern in options.IncludedFilePatterns)
                {
                    if (!string.IsNullOrWhiteSpace(pattern) && GlobMatcher.IsMatch(pattern, relativePath))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    return false;
                }
            }

            if (options.ExcludedFilePatterns is not null)
            {
                foreach (string pattern in options.ExcludedFilePatterns)
                {
                    if (!string.IsNullOrWhiteSpace(pattern) && GlobMatcher.IsMatch(pattern, relativePath))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ReportProgress(IProgress<RepositoryScanProgress> progress, ScanState state)
        {
            if (progress is null)
            {
                return;
            }

            progress.Report(new RepositoryScanProgress
            {
                Phase = ScanPhase.Discovering,
                FilesDiscovered = state.FilesDiscovered,
                FilesProcessed = state.FilesProcessed,
                ReportedUtc = DateTime.UtcNow
            });
        }

        private static string NormaliseRelativePath(string relativePath)
        {
            return relativePath.Replace(CharBackSlash, CharForwardSlash);
        }

        private static string NormaliseExtension(string extension)
        {
            return extension.TrimStart(CharFullStop).ToLowerInvariant();
        }

        private static bool HasIncludedExtension(string filePath, IReadOnlyCollection<string> includedExtensions)
        {
            string extension = NormaliseExtension(Path.GetExtension(filePath));

            foreach (string included in includedExtensions)
            {
                if (string.Equals(extension, NormaliseExtension(included), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExcludedDirectory(string relativePath, IReadOnlyCollection<string> excludedDirectories)
        {
            string[] segments = relativePath.Split([CharForwardSlash], StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                foreach (string excluded in excludedDirectories)
                {
                    if (string.Equals(segment, excluded, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private sealed class ScanState
        {
            internal int FilesDiscovered { get; set; }

            internal int FilesProcessed { get; set; }
        }
    }
}
