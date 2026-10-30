using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Review;
using Revathryn.Engine;
using Revathryn.Review.CSharp;

namespace Revathryn.Review.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ReviewServicesTests
    {
        [TestMethod]
        public async Task CSharpAnalysisEngine_ProducesSymbolJson()
        {
            string root = CreateTempDirectory();
            try
            {
                string sourcePath = Path.Combine(root, "Program.cs");
                File.WriteAllText(sourcePath, "namespace Demo { public class Program { public void Run() { } } }");

                string outputDirectory = Path.Combine(root, "output");
                string manifestPath = Path.Combine(root, "manifest.json");

                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(manifestPath);
                CSharpAnalysisEngine engine = new(analyser, manifestManager);

                string json = await engine.AnalyseSymbolsAsync(sourcePath);

                Assert.IsFalse(string.IsNullOrWhiteSpace(json));
                StringAssert.Contains(json, "Program");

                IManifestEntry entry = manifestManager.GetEntry(sourcePath);
                Assert.IsNotNull(entry);
                Assert.AreEqual("CSharp", entry.Language);
                Assert.AreEqual(1, entry.AnalyserVersion);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CSharpAnalysisEngine_ExposesAnalyserMetadata()
        {
            string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(Path.Combine(outputDirectory, "manifest.json"));
                CSharpAnalysisEngine engine = new(analyser, manifestManager);

                Assert.AreEqual("C# Analyser", engine.Name);
                Assert.AreEqual("CSharp", engine.Language);
                Assert.HasCount(1, engine.FilePatterns);
                Assert.AreEqual("*.cs", engine.FilePatterns[0]);
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        [TestMethod]
        public async Task StubAIReviewEngine_ReturnsNotImplemented()
        {
            StubAIReviewEngine engine = new();

            ReviewResponse response = await engine.ReviewAsync(new ReviewRequest
            {
                RepositoryId = 1,
                RepoFileId = 1,
                ReviewTypeId = 1,
                Language = "CSharp",
                FileContent = "class Program { }"
            });

            Assert.IsFalse(response.Success);
            Assert.IsFalse(string.IsNullOrEmpty(response.Error));
            Assert.HasCount(0, response.Results);
        }

        [TestMethod]
        public async Task StubReviewContextProvider_EchoesContextKey()
        {
            StubReviewContextProvider provider = new();

            ReviewContextResponse response = await provider.GetContextAsync(new ReviewContextRequest
            {
                RepositoryId = 1,
                RepoFileId = 1,
                Language = "CSharp",
                ContextKey = "symbols",
                MaxItems = 10
            });

            Assert.AreEqual("symbols", response.ContextKey);
            Assert.HasCount(0, response.Items);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-review-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
