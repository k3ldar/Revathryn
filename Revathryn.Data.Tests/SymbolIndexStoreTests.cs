using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SymbolIndexStoreTests
    {
        private const string RepositoryName = "Test Repo";

        [TestMethod]
        public async Task UpsertFileAsync_ThenGetSymbolsForFileAsync_ReturnsSymbolWithMembers()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            IndexedSymbol symbol = CreateSymbol(
                "Program",
                "Demo.Program",
                "Class",
                [CreateMember(Consts.MemberKindMethod, "Run", "void Run()", 3, 4)]);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [symbol], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> symbols = await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken);

            Assert.HasCount(1, symbols);
            Assert.AreEqual("Demo.Program", symbols[0].FullyQualifiedName);
            Assert.HasCount(1, symbols[0].Members);
            Assert.AreEqual(Consts.MemberKindMethod, symbols[0].Members[0].Kind);
        }

        [TestMethod]
        public async Task UpsertFileAsync_ThenGetSymbolsForFileAsync_ReturnsUsingDirectives()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            IndexedSymbol symbol = CreateSymbol("Program", "Demo.Program", "Class", []);
            symbol.UsingDirectives = ["System", "Microsoft.Extensions.Logging", "Demo.Orders"];

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [symbol], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> symbols = await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken);

            Assert.HasCount(1, symbols);
            Assert.HasCount(3, symbols[0].UsingDirectives);
            Assert.AreEqual("System", symbols[0].UsingDirectives[0]);
            Assert.AreEqual("Microsoft.Extensions.Logging", symbols[0].UsingDirectives[1]);
            Assert.AreEqual("Demo.Orders", symbols[0].UsingDirectives[2]);
        }

        [TestMethod]
        public async Task UpsertFileAsync_WithSameSourcePath_ReplacesPreviousSymbols()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("Old", "Demo.Old", "Class", [])], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("New", "Demo.New", "Class", [])], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> symbols = await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken);

            Assert.HasCount(1, symbols);
            Assert.AreEqual("Demo.New", symbols[0].FullyQualifiedName);
        }

        [TestMethod]
        public async Task GetSymbolsByNamesAsync_MatchesSimpleAndFullyQualifiedNames()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("Program", "Demo.Program", "Class", [])], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> byName = await store.GetSymbolsByNamesAsync(RepositoryName, ["Program"], TestContext.CancellationToken);
            IReadOnlyList<IndexedSymbol> byQualifiedName = await store.GetSymbolsByNamesAsync(RepositoryName, ["Demo.Program"], TestContext.CancellationToken);

            Assert.HasCount(1, byName);
            Assert.HasCount(1, byQualifiedName);
        }

        [TestMethod]
        public async Task GetSymbolsReferencingAsync_ReturnsDependents()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            IndexedSymbol definition = CreateSymbol("Target", "Demo.Target", "Class", []);
            IndexedSymbol dependent = CreateSymbol("Consumer", "Demo.Consumer", "Class", []);
            dependent.Dependencies = ["Demo.Target"];

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [definition], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.OtherSourcePath), [dependent], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> referencing = await store.GetSymbolsReferencingAsync(RepositoryName, ["Demo.Target"], TestContext.CancellationToken);

            Assert.HasCount(1, referencing);
            Assert.AreEqual("Demo.Consumer", referencing[0].FullyQualifiedName);
        }

        [TestMethod]
        public async Task SearchSymbolsAsync_MatchesNameAndNamespace()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("Program", "Demo.Program", "Class", [])], TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> byName = await store.SearchSymbolsAsync(RepositoryName, "Program", TestContext.CancellationToken);
            IReadOnlyList<IndexedSymbol> byNamespace = await store.SearchSymbolsAsync(RepositoryName, "Demo", TestContext.CancellationToken);

            Assert.HasCount(1, byName);
            Assert.HasCount(1, byNamespace);
        }

        [TestMethod]
        public async Task DeleteFileAsync_RemovesSymbols()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("Program", "Demo.Program", "Class", [])], TestContext.CancellationToken);
            await store.DeleteFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken);

            IReadOnlyList<IndexedSymbol> symbols = await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken);

            Assert.HasCount(0, symbols);
        }

        [TestMethod]
        public async Task DeleteRepositoryAsync_RemovesAllSymbols()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [CreateSymbol("One", "Demo.One", "Class", [])], TestContext.CancellationToken);
            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.OtherSourcePath), [CreateSymbol("Two", "Demo.Two", "Class", [])], TestContext.CancellationToken);

            await store.DeleteRepositoryAsync(RepositoryName, TestContext.CancellationToken);

            Assert.HasCount(0, await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken));
            Assert.HasCount(0, await store.GetSymbolsForFileAsync(RepositoryName, temp.OtherSourcePath, TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task EmptyInputs_ReturnEmptyCollections()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            Assert.HasCount(0, await store.GetSymbolsByNamesAsync(RepositoryName, [], TestContext.CancellationToken));
            Assert.HasCount(0, await store.SearchSymbolsAsync(RepositoryName, string.Empty, TestContext.CancellationToken));
            Assert.HasCount(0, await store.GetSymbolsForFileAsync(RepositoryName, string.Empty, TestContext.CancellationToken));
            Assert.HasCount(0, await store.GetSymbolsReferencingAsync(RepositoryName, [], TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task UpsertFileAsync_WithNoSymbols_StoresFileOnly()
        {
            using TempDirectory temp = new();
            SymbolIndexStore store = new(temp.Path);

            await store.UpsertFileAsync(RepositoryName, CreateFile(temp.SourcePath), [], TestContext.CancellationToken);

            Assert.HasCount(0, await store.GetSymbolsForFileAsync(RepositoryName, temp.SourcePath, TestContext.CancellationToken));
        }

        [TestMethod]
        public void Constructor_NullOrWhitespaceDirectory_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new SymbolIndexStore(null));
            Assert.ThrowsExactly<ArgumentException>(() => new SymbolIndexStore("   "));
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

        private static IndexedSymbol CreateSymbol(string name, string fullyQualifiedName, string kind, List<IndexedMember> members)
        {
            return new IndexedSymbol
            {
                Name = name,
                FullyQualifiedName = fullyQualifiedName,
                Kind = kind,
                FilePath = "source.cs",
                LineNumber = 1,
                EndLineNumber = 5,
                Namespace = "Demo",
                Members = members
            };
        }

        private static IndexedMember CreateMember(string kind, string name, string signature, int lineNumber, int endLineNumber)
        {
            return new IndexedMember
            {
                Kind = kind,
                Name = name,
                Signature = signature,
                LineNumber = lineNumber,
                EndLineNumber = endLineNumber
            };
        }

        public TestContext TestContext { get; set; }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revathryn-symbol-index-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);

                SourcePath = System.IO.Path.Combine(Path, "Program.cs");
                OtherSourcePath = System.IO.Path.Combine(Path, "Other.cs");
                File.WriteAllText(SourcePath, "namespace Demo { public class Program { } }");
                File.WriteAllText(OtherSourcePath, "namespace Demo { public class Other { } }");
            }

            public string Path { get; }

            public string SourcePath { get; }

            public string OtherSourcePath { get; }

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
