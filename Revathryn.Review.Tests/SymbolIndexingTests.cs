using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Engine;
using Revathryn.Review.CSharp;

namespace Revathryn.Review.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SymbolIndexingTests
    {
        private const string RepositoryName = "Demo Repo";

        [TestMethod]
        public async Task CSharpAnalysisEngine_AnalysesAndIndexesSymbols()
        {
            string root = CreateTempDirectory();
            try
            {
                string sourcePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(sourcePath, "namespace Demo { public class Program { public void Run() { } } }");

                string outputDirectory = Path.Combine(root, "output");
                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(outputDirectory);
                FakeSymbolIndexStore store = new();
                CSharpAnalysisEngine engine = new(analyser, manifestManager, store);

                string json = await engine.AnalyseSymbolsAsync(RepositoryName, sourcePath, TestContext.CancellationToken);

                Assert.IsFalse(string.IsNullOrWhiteSpace(json));
                Assert.AreEqual(1, store.UpsertCount);
                Assert.HasCount(1, store.LastSymbols);
                Assert.AreEqual("Demo.Program", store.LastSymbols[0].FullyQualifiedName);
                Assert.HasCount(1, store.LastSymbols[0].Members);
                Assert.AreEqual(Consts.MemberKindMethod, store.LastSymbols[0].Members[0].Kind);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task CSharpAnalysisEngine_MapsUsingDirectivesIntoIndexedSymbol()
        {
            string root = CreateTempDirectory();
            try
            {
                string sourcePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(sourcePath, "using System; using Microsoft.Extensions.Logging; using Demo.Orders; namespace Demo { public class Program { } }");

                string outputDirectory = Path.Combine(root, "output");
                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(outputDirectory);
                FakeSymbolIndexStore store = new();
                CSharpAnalysisEngine engine = new(analyser, manifestManager, store);

                await engine.AnalyseSymbolsAsync(RepositoryName, sourcePath, TestContext.CancellationToken);

                Assert.HasCount(1, store.LastSymbols);
                Assert.HasCount(3, store.LastSymbols[0].UsingDirectives);
                Assert.AreEqual("Demo.Orders", store.LastSymbols[0].UsingDirectives[0]);
                Assert.AreEqual("Microsoft.Extensions.Logging", store.LastSymbols[0].UsingDirectives[1]);
                Assert.AreEqual("System", store.LastSymbols[0].UsingDirectives[2]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task CSharpAnalysisEngine_WithoutStore_SkipsIndexing()
        {
            string root = CreateTempDirectory();
            try
            {
                string sourcePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(sourcePath, "namespace Demo { public class Program { } }");

                string outputDirectory = Path.Combine(root, "output");
                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(outputDirectory);
                CSharpAnalysisEngine engine = new(analyser, manifestManager);

                string json = await engine.AnalyseSymbolsAsync(RepositoryName, sourcePath, TestContext.CancellationToken);

                Assert.IsFalse(string.IsNullOrWhiteSpace(json));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task SymbolContextProvider_UsesIndexQueryWhenAvailable()
        {
            string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                FakeSymbolIndexQuery query = new()
                {
                    Response = new SymbolQueryResponse
                    {
                        ContextKey = Consts.ContextKeySymbols,
                        Candidates =
                        [
                            new SymbolCandidate
                            {
                                FullyQualifiedName = "Demo.Program",
                                Name = "Program",
                                Kind = "Class",
                                FilePath = "Program.cs",
                                Content = "Class Demo.Program",
                                Score = 100
                            }
                        ]
                    }
                };

                SymbolContextProvider provider = new(new ManifestManager(outputDirectory), query);
                ReviewContextRequest request = new()
                {
                    RepositoryName = RepositoryName,
                    SourcePath = "Program.cs",
                    ContextKey = Consts.ContextKeySymbols,
                    MaxItems = 5
                };

                ReviewContextResponse response = await provider.GetContextAsync(request, TestContext.CancellationToken);

                Assert.IsNotNull(query.LastRequest);
                Assert.AreEqual(Consts.ContextKeySymbols, query.LastRequest.ContextKey);
                Assert.HasCount(1, response.Items);
                Assert.AreEqual("Demo.Program", response.Items[0].Title);
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-review-index-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public TestContext TestContext { get; set; }

        private sealed class FakeSymbolIndexStore : ISymbolIndexStore
        {
            public int UpsertCount { get; private set; }

            public IndexedFile LastFile { get; private set; }

            public IReadOnlyList<IndexedSymbol> LastSymbols { get; private set; } = [];

            public Task UpsertFileAsync(string repositoryName, IndexedFile file, IReadOnlyList<IndexedSymbol> symbols, CancellationToken cancellationToken = default)
            {
                LastFile = file;
                LastSymbols = symbols;
                UpsertCount++;
                return Task.CompletedTask;
            }

            public Task DeleteFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsForFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsByNamesAsync(string repositoryName, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> GetSymbolsReferencingAsync(string repositoryName, IReadOnlyCollection<string> targetNames, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }

            public Task<IReadOnlyList<IndexedSymbol>> SearchSymbolsAsync(string repositoryName, string searchText, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<IndexedSymbol>>([]);
            }
        }

        private sealed class FakeSymbolIndexQuery : ISymbolIndexQuery
        {
            public SymbolQueryResponse Response { get; set; } = new();

            public SymbolQueryRequest LastRequest { get; private set; }

            public Task<SymbolQueryResponse> QueryAsync(string repositoryName, SymbolQueryRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult(Response);
            }
        }
    }
}
