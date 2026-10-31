using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Engine;
using Revathryn.Review;
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
        public async Task OpenAIReviewEngine_ValidJsonResponse_ReturnsResults()
        {
            // Arrange
            AIEngine configuredEngine = new()
            {
                Id = 1,
                Name = "OpenAI",
                Provider = AIProvider.OpenAI,
                Endpoint = "https://example.test/v1",
                Model = "test-model",
                SecretKeyName = "test-secret",
                Enabled = true,
                IsDefault = true
            };

            string completionBody = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"results\":[{\"fingerprint\":\"fp-1\",\"severity\":\"High\",\"title\":\"Title\",\"description\":\"Description\",\"suggestedFix\":\"Fix\"}],\"contextRequests\":[]}"
                      }
                    }
                  ]
                }
                """;

            StubHttpMessageHandler handler = new(HttpStatusCode.OK, completionBody, "application/json");
            HttpClient httpClient = new(handler);

            OpenAIReviewEngine engine = new(
                new FakeAIEngineRepository(configuredEngine),
                new FakeSecretsProvider("api-key"),
                httpClient);

            ReviewRequest request = new()
            {
                RepositoryId = 1,
                RepoFileId = 1,
                ReviewTypeId = 1,
                Language = "CSharp",
                FileContent = "class Program { }",
                Prompt = "Review this file."
            };

            // Act
            ReviewResponse response = await engine.ReviewAsync(request);

            // Assert
            Assert.IsTrue(response.Success);
            Assert.IsEmpty(response.Error);
            Assert.HasCount(1, response.Results);
            Assert.AreEqual("fp-1", response.Results[0].Fingerprint);
            Assert.AreEqual(FindingSeverity.High, response.Results[0].Severity);
            Assert.AreEqual("Title", response.Results[0].Title);
        }

        [TestMethod]
        public async Task OpenAIReviewEngine_MalformedJsonResponse_ReturnsFailure()
        {
            // Arrange
            AIEngine configuredEngine = new()
            {
                Id = 1,
                Name = "OpenAI",
                Provider = AIProvider.OpenAI,
                Endpoint = "https://example.test/v1",
                Model = "test-model",
                SecretKeyName = "test-secret",
                Enabled = true,
                IsDefault = true
            };

            string completionBody = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "this is not json"
                      }
                    }
                  ]
                }
                """;

            StubHttpMessageHandler handler = new(HttpStatusCode.OK, completionBody, "application/json");
            HttpClient httpClient = new(handler);

            OpenAIReviewEngine engine = new(
                new FakeAIEngineRepository(configuredEngine),
                new FakeSecretsProvider("api-key"),
                httpClient);

            ReviewRequest request = new()
            {
                RepositoryId = 1,
                RepoFileId = 1,
                ReviewTypeId = 1,
                Language = "CSharp",
                FileContent = "class Program { }",
                Prompt = "Review this file."
            };

            // Act
            ReviewResponse response = await engine.ReviewAsync(request);

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Error));
            Assert.HasCount(0, response.Results);
        }

        [TestMethod]
        public async Task SymbolContextProvider_SymbolContextKey_ReturnsSymbolItems()
        {
            // Arrange
            string root = CreateTempDirectory();
            try
            {
                string sourcePath = Path.Combine(root, "Program.cs");
                string outputFile = Path.Combine(root, "symbols.json");

                List<CodeSymbol> symbols =
                [
                    new CodeSymbol
                    {
                        Name = "Program",
                        FullyQualifiedName = "Demo.Program",
                        FilePath = sourcePath,
                        Dependencies = ["System", "System.Text"]
                    }
                ];

                File.WriteAllText(outputFile, JsonSerializer.Serialize(symbols));

                FakeManifestManager manifestManager = new(new ManifestEntryStub
                {
                    OutputFile = outputFile,
                    SourcePath = sourcePath
                });

                SymbolContextProvider provider = new(manifestManager);

                ReviewContextRequest request = new()
                {
                    RepositoryId = 1,
                    RepoFileId = 1,
                    Language = "CSharp",
                    ContextKey = Consts.ContextKeySymbols,
                    MaxItems = 10,
                    SourcePath = sourcePath
                };

                // Act
                ReviewContextResponse response = await provider.GetContextAsync(request);

                // Assert
                Assert.AreEqual(Consts.ContextKeySymbols, response.ContextKey);
                Assert.HasCount(1, response.Items);
                Assert.AreEqual("Demo.Program", response.Items[0].Title);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "revathryn-review-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;
            private readonly string _mediaType;

            public StubHttpMessageHandler(HttpStatusCode statusCode, string body, string mediaType)
            {
                _statusCode = statusCode;
                _body = body;
                _mediaType = mediaType;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new(_statusCode)
                {
                    Content = new StringContent(_body, Encoding.UTF8, _mediaType)
                };

                return Task.FromResult(response);
            }
        }

        private sealed class FakeAIEngineRepository : IAIEngineRepository
        {
            private readonly AIEngine _engine;

            public FakeAIEngineRepository(AIEngine engine)
            {
                _engine = engine;
            }

            public Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_engine.Id == id ? _engine : null);
            }

            public Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AIEngine>>([_engine]);
            }

            public Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<AIEngine>>(_engine.Enabled ? [_engine] : []);
            }

            public Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_engine.IsDefault ? _engine : null);
            }

            public Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(engine.Id);
            }

            public Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSecretsProvider : ISecretsProvider
        {
            private readonly string _secret;

            public FakeSecretsProvider(string secret)
            {
                _secret = secret;
            }

            public Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_secret);
            }

            public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeManifestManager : IManifestManager
        {
            private readonly IManifestEntry _entry;

            public FakeManifestManager(IManifestEntry entry)
            {
                _entry = entry;
            }

            public bool RequiresAnalysis(string filePath, int currentAnalyserVersion)
            {
                return false;
            }

            public void RecordAnalysis(IManifestEntry entry)
            {
            }

            public IManifestEntry GetEntry(string filePath)
            {
                return string.Equals(filePath, _entry.SourcePath, StringComparison.OrdinalIgnoreCase) ? _entry : null;
            }
        }

        private sealed class ManifestEntryStub : IManifestEntry
        {
            public DateTime AnalysedUtc { get; set; }
            public int AnalyserVersion { get; set; }
            public string Language { get; set; } = string.Empty;
            public string LanguageVersion { get; set; } = string.Empty;
            public string OutputFile { get; set; } = string.Empty;
            public string SourceHash { get; set; } = string.Empty;
            public DateTime SourceLastWriteUtc { get; set; }
            public string SourcePath { get; set; } = string.Empty;
        }
    }
}
