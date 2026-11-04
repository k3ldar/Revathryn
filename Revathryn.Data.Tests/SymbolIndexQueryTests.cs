using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SymbolIndexQueryTests
    {
        private const string RepositoryName = "Query Repo";

        [TestMethod]
        public async Task QueryAsync_Related_ReturnsCompactUsageSummary()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            IndexedSymbol reviewed = CreateSymbol("Program", "Demo.Program", "Class", temp.SourcePath);
            IndexedSymbol consumer = CreateSymbol("Consumer", "Demo.Consumer", "Class", temp.OtherSourcePath);
            consumer.Dependencies = ["Demo.Program"];

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [reviewed], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.OtherSourcePath), [consumer], TestContext.CancellationToken);

            SymbolIndexQuery query = new(store);
            SymbolQueryResponse response = await query.QueryAsync(RepositoryName, new SymbolQueryRequest
            {
                ContextKey = Consts.ContextKeyRelated,
                SourcePath = temp.SourcePath,
                MaxItems = 10
            }, TestContext.CancellationToken);

            Assert.AreEqual(Consts.ContextKeyRelated, response.ContextKey);
            Assert.HasCount(1, response.Candidates);
            Assert.AreEqual("Demo.Consumer", response.Candidates[0].FullyQualifiedName);
            Assert.AreEqual(Consts.ContextKeyRelated, response.Candidates[0].Reason);
            Assert.IsTrue(response.Candidates[0].Content.Contains("is used by", StringComparison.Ordinal));
            Assert.IsTrue(response.Candidates[0].Content.Contains("Demo.Consumer", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task QueryAsync_Files_FiltersByRequestedFileName()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            IndexedSymbol reviewed = CreateSymbol("Program", "Demo.Program", "Class", temp.SourcePath);
            IndexedSymbol consumer = CreateSymbol("Consumer", "Demo.Consumer", "Class", temp.OtherSourcePath);
            consumer.Dependencies = ["Demo.Program"];
            IndexedSymbol runner = CreateSymbol("Runner", "Demo.Runner", "Class", temp.ThirdSourcePath);
            runner.Dependencies = ["Demo.Program"];

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [reviewed], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.OtherSourcePath), [consumer], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.ThirdSourcePath), [runner], TestContext.CancellationToken);

            SymbolIndexQuery query = new(store);
            SymbolQueryResponse response = await query.QueryAsync(RepositoryName, new SymbolQueryRequest
            {
                ContextKey = Consts.ContextKeyFiles,
                SourcePath = temp.SourcePath,
                Query = "Consumer.cs",
                MaxItems = 10
            }, TestContext.CancellationToken);

            Assert.AreEqual(Consts.ContextKeyFiles, response.ContextKey);
            Assert.HasCount(1, response.Candidates);
            Assert.AreEqual("Demo.Consumer", response.Candidates[0].FullyQualifiedName);
        }

        private static IndexedFile CreateFile(string sourcePath)
        {
            return new IndexedFile
            {
                SourcePath = sourcePath,
                SourceHash = "hash",
                SourceLastWriteUtc = DateTime.UtcNow,
                AnalysedUtc = DateTime.UtcNow,
                AnalyserVersion = 1,
                Language = Consts.LanguageType,
                LanguageVersion = Consts.LanguageVersionNumber
            };
        }

        private static IndexedSymbol CreateSymbol(string name, string fullyQualifiedName, string kind, string filePath)
        {
            return new IndexedSymbol
            {
                Name = name,
                FullyQualifiedName = fullyQualifiedName,
                Kind = kind,
                FilePath = filePath,
                LineNumber = 10,
                EndLineNumber = 12,
                Namespace = "Demo"
            };
        }

        public TestContext TestContext { get; set; }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revathryn-symbol-query-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);

                SourcePath = System.IO.Path.Combine(Path, "Program.cs");
                OtherSourcePath = System.IO.Path.Combine(Path, "Consumer.cs");
                ThirdSourcePath = System.IO.Path.Combine(Path, "Runner.cs");
                File.WriteAllText(SourcePath, "namespace Demo { public class Program { } }");
                File.WriteAllText(OtherSourcePath, "namespace Demo { public class Consumer { } }");
                File.WriteAllText(ThirdSourcePath, "namespace Demo { public class Runner { } }");
            }

            public string Path { get; }

            public string SourcePath { get; }

            public string OtherSourcePath { get; }

            public string ThirdSourcePath { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
        }
    }
}
