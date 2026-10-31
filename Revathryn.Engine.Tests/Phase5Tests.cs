using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class Phase5Tests
    {
        private const string EmptyFileSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        [TestMethod]
        public async Task FileHasher_EmptyFile_ReturnsKnownSha256()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "empty.cs");
                File.WriteAllText(filePath, string.Empty);

                IFileHasher hasher = new FileHasher();

                string hash = await hasher.ComputeHashAsync(filePath);

                Assert.AreEqual(EmptyFileSha256, hash);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task FileHasher_IdenticalContent_ProducesIdenticalHashes()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "same.cs");
                File.WriteAllText(filePath, "class Same { }");

                IFileHasher hasher = new FileHasher();

                string synchronous = hasher.ComputeHash(filePath);
                string asynchronous = await hasher.ComputeHashAsync(filePath);

                Assert.AreEqual(synchronous, asynchronous);
                Assert.AreEqual(64, synchronous.Length);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task FileHasher_ChangedContent_ProducesDifferentHashes()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "changed.cs");
                File.WriteAllText(filePath, "class Changed { }");

                IFileHasher hasher = new FileHasher();
                string first = await hasher.ComputeHashAsync(filePath);

                File.WriteAllText(filePath, "class Changed { void M() { } }");
                string second = await hasher.ComputeHashAsync(filePath);

                Assert.AreNotEqual(first, second);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task FileHasher_LargeFile_ProducesConsistentHash()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "large.cs");
                byte[] content = new byte[8 * 1024 * 1024];

                for (int index = 0; index < content.Length; index++)
                {
                    content[index] = (byte)(index % 251);
                }

                File.WriteAllBytes(filePath, content);

                IFileHasher hasher = new FileHasher();

                string synchronous = hasher.ComputeHash(filePath);
                string asynchronous = await hasher.ComputeHashAsync(filePath);

                Assert.AreEqual(synchronous, asynchronous);
                Assert.AreEqual(64, synchronous.Length);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task FileHasher_NonexistentFile_ThrowsFileNotFound()
        {
            string root = CreateTempDirectory();
            try
            {
                string missing = Path.Combine(root, "missing.cs");
                IFileHasher hasher = new FileHasher();

                await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => hasher.ComputeHashAsync(missing));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task FileHasher_CancelledToken_ThrowsOperationCanceled()
        {
            string root = CreateTempDirectory();
            try
            {
                string filePath = Path.Combine(root, "cancel.cs");
                File.WriteAllText(filePath, "class Cancel { }");

                IFileHasher hasher = new FileHasher();
                using CancellationTokenSource source = new();
                source.Cancel();

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => hasher.ComputeHashAsync(filePath, source.Token));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_DiscoversMultipleExtensions()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");
                File.WriteAllText(Path.Combine(root, "settings.json"), "{}");
                File.WriteAllText(Path.Combine(root, "config.xml"), "<root />");
                File.WriteAllText(Path.Combine(root, "readme.md"), "# Readme");
                File.WriteAllText(Path.Combine(root, "pipeline.yml"), "steps: []");
                File.WriteAllText(Path.Combine(root, "app.config"), "<configuration />");

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);

                Assert.HasCount(6, scan.DiscoveredFiles);
                CollectionAssert.AreEquivalent(
                    new[] { "cs", "json", "xml", "md", "yml", "config" },
                    scan.DiscoveredFiles.Select(file => file.Extension).ToArray());
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_AppliesCustomIncludedExtensions()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");
                File.WriteAllText(Path.Combine(root, "readme.md"), "# Readme");

                RepositoryScanOptions options = new()
                {
                    IncludedExtensions = ["md"]
                };

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, options);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("readme.md", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_AppliesIncludedFilePatterns()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
                File.WriteAllText(Path.Combine(root, "src", "A.cs"), "class A { }");
                File.WriteAllText(Path.Combine(root, "src", "nested", "B.cs"), "class B { }");
                File.WriteAllText(Path.Combine(root, "Other.cs"), "class Other { }");

                RepositoryScanOptions options = new()
                {
                    IncludedFilePatterns = ["src/**/*.cs"]
                };

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, options);

                Assert.HasCount(2, scan.DiscoveredFiles);
                CollectionAssert.AreEquivalent(
                    new[] { "src/A.cs", "src/nested/B.cs" },
                    scan.DiscoveredFiles.Select(file => file.RelativePath).ToArray());
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_AppliesExcludedFilePatterns()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "generated"));
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                File.WriteAllText(Path.Combine(root, "generated", "ignored.cs"), "class Ignored { }");

                RepositoryScanOptions options = new()
                {
                    ExcludedFilePatterns = ["**/generated/**"]
                };

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, options);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("keep.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_RespectsGitIgnore()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, ".gitignore"), "*.generated.cs\n!important.generated.cs\n");
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                File.WriteAllText(Path.Combine(root, "ignored.generated.cs"), "class Ignored { }");
                File.WriteAllText(Path.Combine(root, "important.generated.cs"), "class Important { }");

                RepositoryScanOptions options = new()
                {
                    RespectGitIgnore = true
                };

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root, options);

                CollectionAssert.AreEquivalent(
                    new[] { "keep.cs", "important.generated.cs" },
                    scan.DiscoveredFiles.Select(file => file.RelativePath).ToArray());
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ExcludesBinaryFiles()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");
                File.WriteAllBytes(Path.Combine(root, "binary.cs"), [0x00, 0x01, 0x02, 0x00, 0x03]);

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("keep.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_IgnoresEmptyDirectories()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "empty"));
                File.WriteAllText(Path.Combine(root, "keep.cs"), "class Keep { }");

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);

                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.HasCount(0, scan.Failures);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_SkipsSymbolicLinks()
        {
            string root = CreateTempDirectory();
            string outsideRoot = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(outsideRoot, "Linked.cs"), "class Linked { }");

                string link = Path.Combine(root, "link");

                try
                {
                    Directory.CreateSymbolicLink(link, outsideRoot);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
                {
                    Assert.Inconclusive("Symbolic links require elevated privileges or Developer Mode on this platform.");
                    return;
                }

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);

                Assert.HasCount(0, scan.DiscoveredFiles);
            }
            finally
            {
                Directory.Delete(root, true);
                Directory.Delete(outsideRoot, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_ReportsProgress()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                List<RepositoryScanProgress> reports = [];
                SynchronousProgress<RepositoryScanProgress> progress = new(reports.Add);

                RepositoryScanner scanner = new();
                await scanner.ScanAsync(root, progress: progress);

                Assert.IsNotEmpty(reports);
                Assert.IsTrue(reports.TrueForAll(report => report.Phase == ScanPhase.Discovering));
                Assert.IsTrue(reports.Exists(report => report.FilesDiscovered > 0));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RepositoryScanner_CancelledToken_ThrowsOperationCanceled()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                RepositoryScanner scanner = new();
                using CancellationTokenSource source = new();
                source.Cancel();

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => scanner.ScanAsync(root, cancellationToken: source.Token));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_StandaloneProject()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "MyApp.csproj"), "<Project />");

                IProjectDiscoveryService service = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await service.DiscoverAsync(root);

                Assert.HasCount(1, projects);
                Assert.AreEqual(ProjectKind.Project, projects[0].Kind);
                Assert.AreEqual("MyApp", projects[0].Name);
                Assert.AreEqual("MyApp.csproj", projects[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_SolutionWithMultipleProjects()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "App"));
                Directory.CreateDirectory(Path.Combine(root, "src", "Lib"));
                File.WriteAllText(Path.Combine(root, "src", "App", "App.csproj"), "<Project />");
                File.WriteAllText(Path.Combine(root, "src", "Lib", "Lib.csproj"), "<Project />");

                string solution =
                    "Microsoft Visual Studio Solution File, Format Version 12.00\n"
                    + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"App\", \"src\\App\\App.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\n"
                    + "EndProject\n"
                    + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Lib\", \"src\\Lib\\Lib.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\n"
                    + "EndProject\n";

                File.WriteAllText(Path.Combine(root, "App.sln"), solution);

                IProjectDiscoveryService service = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await service.DiscoverAsync(root);

                Assert.HasCount(3, projects);

                RepositoryProject solutionProject = projects.Single(project => project.Kind == ProjectKind.Solution);
                Assert.AreEqual("App.sln", solutionProject.RelativePath);

                IReadOnlyList<RepositoryProject> appProjects = [.. projects.Where(project => project.Kind == ProjectKind.Project)];

                Assert.HasCount(2, appProjects);
                Assert.IsTrue(appProjects.All(project => project.SolutionPath == "App.sln"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_NestedProject()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "Nested"));
                File.WriteAllText(Path.Combine(root, "src", "Nested", "Inner.csproj"), "<Project />");

                IProjectDiscoveryService service = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await service.DiscoverAsync(root);

                Assert.HasCount(1, projects);
                Assert.AreEqual("src/Nested/Inner.csproj", projects[0].RelativePath);
                Assert.AreEqual("src/Nested", projects[0].DirectoryPath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_RepositoryWithoutSolution_ReturnsEmpty()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");
                File.WriteAllText(Path.Combine(root, "readme.md"), "# Readme");

                IProjectDiscoveryService service = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await service.DiscoverAsync(root);

                Assert.HasCount(0, projects);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_SlnxSolution()
        {
            string root = CreateTempDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "src", "App"));
                File.WriteAllText(Path.Combine(root, "src", "App", "App.csproj"), "<Project />");
                File.WriteAllText(Path.Combine(root, "App.slnx"), "<Solution><Project Path=\"src/App/App.csproj\" /></Solution>");

                IProjectDiscoveryService service = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await service.DiscoverAsync(root);

                Assert.HasCount(2, projects);

                RepositoryProject solution = projects.Single(project => project.Kind == ProjectKind.Solution);
                RepositoryProject app = projects.Single(project => project.Kind == ProjectKind.Project);

                Assert.AreEqual("App.slnx", solution.RelativePath);
                Assert.AreEqual("App.slnx", app.SolutionPath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task ProjectDiscoveryService_ProjectlessFiles_RemainScannable()
        {
            string root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");

                IProjectDiscoveryService discovery = new ProjectDiscoveryService();
                IReadOnlyList<RepositoryProject> projects = await discovery.DiscoverAsync(root);

                RepositoryScanner scanner = new();
                RepositoryScanResult scan = await scanner.ScanAsync(root);

                Assert.HasCount(0, projects);
                Assert.HasCount(1, scan.DiscoveredFiles);
                Assert.AreEqual("Program.cs", scan.DiscoveredFiles[0].RelativePath);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-phase5-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;

            public SynchronousProgress(Action<T> handler)
            {
                _handler = handler;
            }

            public void Report(T value)
            {
                _handler(value);
            }
        }
    }
}
