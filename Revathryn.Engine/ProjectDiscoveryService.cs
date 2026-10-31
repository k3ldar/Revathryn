using System.Xml.Linq;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Discovers solutions and projects within a local repository.</summary>
    public sealed class ProjectDiscoveryService : IProjectDiscoveryService
    {
        private const string SolutionProjectLinePrefix = "Project(";
        private const int SolutionProjectQuotedPartsCount = 3;
        private const int SolutionProjectPathIndex = 2;
        private const string SlnxProjectElementName = "Project";
        private const string SlnxProjectPathAttribute = "Path";
        private const char DoubleQuoteCharacter = '"';

        private static readonly string[] _excludedDirectories =
        [
            FilePathGit, FilePathVSInternal, FilePathBin, FilePathObj, FilePathNodeModules, FilePathPackages, FilePathBuild, FilePathDist
        ];

        /// <summary>Returns solutions and projects discovered beneath the given root.</summary>
        public Task<IReadOnlyList<RepositoryProject>> DiscoverAsync(string localPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException(Strings.PathRequired, nameof(localPath));
            }

            if (!Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException(string.Format(Strings.RepositoryFolderNotFound, localPath));
            }

            string fullRoot = Path.GetFullPath(localPath);
            List<RepositoryProject> projects = [];
            List<SolutionFile> solutions = [];

            DiscoverDirectory(fullRoot, string.Empty, projects, solutions, cancellationToken);
            ApplySolutionLinks(projects, solutions, fullRoot);

            return Task.FromResult<IReadOnlyList<RepositoryProject>>(projects);
        }

        private static void DiscoverDirectory(
            string fullRoot,
            string relativeDirectory,
            List<RepositoryProject> projects,
            List<SolutionFile> solutions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string currentDirectory = relativeDirectory.Length == 0
                ? fullRoot
                : Path.Combine(fullRoot, relativeDirectory.Replace(CharForwardSlash, Path.DirectorySeparatorChar));

            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(currentDirectory);
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

                bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

                if (isReparsePoint)
                {
                    continue;
                }

                string relativePath = NormaliseRelativePath(Path.GetRelativePath(fullRoot, entry));
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;

                if (isDirectory)
                {
                    if (IsExcludedDirectory(relativePath))
                    {
                        continue;
                    }

                    DiscoverDirectory(fullRoot, relativePath, projects, solutions, cancellationToken);
                    continue;
                }

                string extension = NormaliseExtension(Path.GetExtension(entry));

                if (string.Equals(extension, FileTypeSolution, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, FileTypeSolutionXml, StringComparison.OrdinalIgnoreCase))
                {
                    RepositoryProject solution = BuildProject(ProjectKind.Solution, relativePath);
                    projects.Add(solution);
                    solutions.Add(new SolutionFile(solution, ParseSolutionProjectPaths(extension, entry)));
                    continue;
                }

                if (string.Equals(extension, FileTypeCSharpProj, StringComparison.OrdinalIgnoreCase))
                {
                    projects.Add(BuildProject(ProjectKind.Project, relativePath));
                }
            }
        }

        private static RepositoryProject BuildProject(ProjectKind kind, string relativePath)
        {
            return new RepositoryProject
            {
                Kind = kind,
                Name = Path.GetFileNameWithoutExtension(relativePath.Replace(CharForwardSlash, Path.DirectorySeparatorChar)),
                RelativePath = relativePath,
                DirectoryPath = GetDirectory(relativePath)
            };
        }

        private static List<string> ParseSolutionProjectPaths(string extension, string solutionFilePath)
        {
            if (string.Equals(extension, FileTypeSolutionXml, StringComparison.OrdinalIgnoreCase))
            {
                return ParseSlnxProjectPaths(solutionFilePath);
            }

            return ParseSlnProjectPaths(solutionFilePath);
        }

        private static List<string> ParseSlnProjectPaths(string solutionFilePath)
        {
            List<string> paths = [];

            foreach (string line in File.ReadLines(solutionFilePath))
            {
                string trimmed = line.TrimStart();

                if (!trimmed.StartsWith(SolutionProjectLinePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                List<string> quotedValues = ExtractQuotedValues(trimmed);

                if (quotedValues.Count >= SolutionProjectQuotedPartsCount && quotedValues[SolutionProjectPathIndex].Length > 0)
                {
                    paths.Add(NormaliseRelativePath(quotedValues[SolutionProjectPathIndex]));
                }
            }

            return paths;
        }

        private static List<string> ParseSlnxProjectPaths(string solutionFilePath)
        {
            List<string> paths = [];

            XDocument document = XDocument.Load(solutionFilePath);

            foreach (XElement projectElement in document.Descendants().Where(e => e.Name.LocalName == SlnxProjectElementName))
            {
                XAttribute pathAttribute = projectElement.Attribute(SlnxProjectPathAttribute);

                if (pathAttribute is not null && pathAttribute.Value.Length > 0)
                {
                    paths.Add(NormaliseRelativePath(pathAttribute.Value));
                }
            }

            return paths;
        }

        private static List<string> ExtractQuotedValues(string line)
        {
            List<string> values = [];
            int index = 0;

            while (index < line.Length)
            {
                int start = line.IndexOf(DoubleQuoteCharacter, index);

                if (start < 0)
                {
                    break;
                }

                int end = line.IndexOf(DoubleQuoteCharacter, start + 1);

                if (end < 0)
                {
                    break;
                }

                values.Add(line.Substring(start + 1, end - start - 1));
                index = end + 1;
            }

            return values;
        }

        private static void ApplySolutionLinks(List<RepositoryProject> projects, List<SolutionFile> solutions, string fullRoot)
        {
            foreach (SolutionFile solution in solutions)
            {
                foreach (string projectPath in solution.ProjectPaths)
                {
                    string resolvedPath = ResolveRelativePath(fullRoot, solution.Solution.DirectoryPath, projectPath);

                    RepositoryProject project = projects.FirstOrDefault(p =>
                        p.Kind == ProjectKind.Project
                        && string.Equals(p.RelativePath, resolvedPath, StringComparison.OrdinalIgnoreCase));

                    if (project is not null && project.SolutionPath.Length == 0)
                    {
                        project.SolutionPath = solution.Solution.RelativePath;
                    }
                }
            }
        }

        private static string ResolveRelativePath(string fullRoot, string baseDirectory, string path)
        {
            string osBase = baseDirectory.Length == 0
                ? fullRoot
                : Path.Combine(fullRoot, baseDirectory.Replace(CharForwardSlash, Path.DirectorySeparatorChar));

            string osPath = path.Replace(CharForwardSlash, Path.DirectorySeparatorChar);
            string combined = Path.GetFullPath(Path.Combine(osBase, osPath));
            return NormaliseRelativePath(Path.GetRelativePath(fullRoot, combined));
        }

        private static bool IsExcludedDirectory(string relativePath)
        {
            string[] segments = relativePath.Split([CharForwardSlash], StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                foreach (string excluded in _excludedDirectories)
                {
                    if (string.Equals(segment, excluded, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
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

        private static string NormaliseRelativePath(string relativePath)
        {
            return relativePath.Replace(CharBackSlash, CharForwardSlash);
        }

        private static string NormaliseExtension(string extension)
        {
            return extension.TrimStart(CharFullStop).ToLowerInvariant();
        }

        private sealed class SolutionFile
        {
            internal SolutionFile(RepositoryProject solution, List<string> projectPaths)
            {
                Solution = solution;
                ProjectPaths = projectPaths;
            }

            internal RepositoryProject Solution { get; }

            internal List<string> ProjectPaths { get; }
        }
    }
}
