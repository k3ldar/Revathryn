using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
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
                const string repositoryName = "Demo Repo";

                CSharpAnalyser analyser = new(outputDirectory);
                ManifestManager manifestManager = new(outputDirectory);
                CSharpAnalysisEngine engine = new(analyser, manifestManager);

                string json = await engine.AnalyseSymbolsAsync(repositoryName, sourcePath, TestContext.CancellationToken);

                Assert.IsFalse(string.IsNullOrWhiteSpace(json));
                Assert.Contains("Program", json);

                IManifestEntry entry = manifestManager.GetEntry(repositoryName, sourcePath);
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
                ManifestManager manifestManager = new(outputDirectory);
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
                  ],
                  "usage": {
                    "prompt_tokens": 120,
                    "completion_tokens": 30,
                    "total_tokens": 150,
                    "prompt_tokens_details": { "cached_tokens": 60 }
                  }
                }
                """;

            StubHttpMessageHandler handler = new(HttpStatusCode.OK, completionBody, "application/json");
            HttpClient httpClient = new(handler);

            OpenAIReviewEngine engine = new(
                new FakeAIEngineRepository(configuredEngine),
                new FakeSecretsProvider("api-key"),
                httpClient,
                new FakeOperationalDiagnostics());

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
            ReviewResponse response = await engine.ReviewAsync(request, TestContext.CancellationToken);

            // Assert
            Assert.IsTrue(response.Success);
            Assert.IsEmpty(response.Error);
            Assert.HasCount(1, response.Results);
            Assert.AreEqual("fp-1", response.Results[0].Fingerprint);
            Assert.AreEqual(FindingSeverity.High, response.Results[0].Severity);
            Assert.AreEqual("Title", response.Results[0].Title);
            Assert.AreEqual(1, response.EngineId);
            Assert.AreEqual("OpenAI", response.EngineName);
            Assert.AreEqual("test-model", response.EngineModel);
            Assert.IsNotNull(response.Usage);
            Assert.AreEqual(1, response.Usage.AttemptCount);
            Assert.AreEqual(120, response.Usage.PromptTokens);
            Assert.AreEqual(30, response.Usage.CompletionTokens);
            Assert.AreEqual(150, response.Usage.TotalTokens);
            Assert.AreEqual(60, response.Usage.CachedTokens);
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
                  ],
                  "usage": {
                    "prompt_tokens": 10,
                    "completion_tokens": 5,
                    "total_tokens": 15,
                    "cached_tokens": 2
                  }
                }
                """;

            StubHttpMessageHandler handler = new(HttpStatusCode.OK, completionBody, "application/json");
            HttpClient httpClient = new(handler);

            OpenAIReviewEngine engine = new(
                new FakeAIEngineRepository(configuredEngine),
                new FakeSecretsProvider("api-key"),
                httpClient,
                new FakeOperationalDiagnostics());

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
            ReviewResponse response = await engine.ReviewAsync(request, TestContext.CancellationToken);

            // Assert
            Assert.IsFalse(response.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Error));
            Assert.HasCount(0, response.Results);
            Assert.AreEqual(2, handler.RequestCount);
            Assert.AreEqual(1, response.EngineId);
            Assert.AreEqual("OpenAI", response.EngineName);
            Assert.IsNotNull(response.Usage);
            Assert.AreEqual(2, response.Usage.AttemptCount);
            Assert.AreEqual(20, response.Usage.PromptTokens);
            Assert.AreEqual(10, response.Usage.CompletionTokens);
            Assert.AreEqual(30, response.Usage.TotalTokens);
            Assert.AreEqual(4, response.Usage.CachedTokens);
        }

        [TestMethod]
        public async Task OpenAIReviewEngine_InvalidJsonThenValid_ReturnsResultsAfterOneRetry()
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

            string invalidCompletionBody = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "this is not json"
                      }
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 10,
                    "completion_tokens": 5,
                    "total_tokens": 15,
                    "cached_tokens": 2
                  }
                }
                """;

            string validCompletionBody = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"results\":[{\"fingerprint\":\"fp-1\",\"severity\":\"High\",\"title\":\"Title\",\"description\":\"Description\",\"suggestedFix\":\"Fix\"}],\"contextRequests\":[]}"
                      }
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 120,
                    "completion_tokens": 30,
                    "total_tokens": 150,
                    "prompt_tokens_details": { "cached_tokens": 60 }
                  }
                }
                """;

            StubHttpMessageHandler handler = new(
            [
                new StubResponse(HttpStatusCode.OK, invalidCompletionBody, "application/json"),
                new StubResponse(HttpStatusCode.OK, validCompletionBody, "application/json")
            ]);
            HttpClient httpClient = new(handler);

            OpenAIReviewEngine engine = new(
                new FakeAIEngineRepository(configuredEngine),
                new FakeSecretsProvider("api-key"),
                httpClient,
                new FakeOperationalDiagnostics());

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
            ReviewResponse response = await engine.ReviewAsync(request, TestContext.CancellationToken);

            // Assert
            Assert.IsTrue(response.Success);
            Assert.HasCount(1, response.Results);
            Assert.AreEqual("fp-1", response.Results[0].Fingerprint);
            Assert.AreEqual(2, handler.RequestCount);
            Assert.Contains("not valid JSON", handler.RequestBodies[1]);
            Assert.AreEqual(1, response.EngineId);
            Assert.AreEqual("OpenAI", response.EngineName);
            Assert.IsNotNull(response.Usage);
            Assert.AreEqual(2, response.Usage.AttemptCount);
            Assert.AreEqual(130, response.Usage.PromptTokens);
            Assert.AreEqual(35, response.Usage.CompletionTokens);
            Assert.AreEqual(165, response.Usage.TotalTokens);
            Assert.AreEqual(62, response.Usage.CachedTokens);
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
                    RepositoryName = "Demo Repo",
                    RepoFileId = 1,
                    Language = "CSharp",
                    ContextKey = Consts.ContextKeySymbols,
                    MaxItems = 10,
                    SourcePath = sourcePath
                };

                // Act
                ReviewContextResponse response = await provider.GetContextAsync(request, TestContext.CancellationToken);

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

        [TestMethod]
        public async Task SymbolContextProvider_FilesContextKey_ReturnsReferencingFiles()
        {
            // Arrange
            string root = CreateTempDirectory();
            try
            {
                string reviewedPath = Path.Combine(root, "ReviewProcessor.cs");
                string consumerPath = Path.Combine(root, "ReviewWorkerService.cs");
                string reviewedOutputFile = Path.Combine(root, "reviewed-symbols.json");
                string consumerOutputFile = Path.Combine(root, "consumer-symbols.json");

                List<CodeSymbol> reviewedSymbols =
                [
                    new CodeSymbol
                    {
                        Name = "ReviewProcessor",
                        FullyQualifiedName = "Demo.ReviewProcessor",
                        Kind = SymbolKind.Class,
                        FilePath = reviewedPath
                    }
                ];

                List<CodeSymbol> consumerSymbols =
                [
                    new CodeSymbol
                    {
                        Name = "ReviewWorkerService",
                        FullyQualifiedName = "Demo.ReviewWorkerService",
                        Kind = SymbolKind.Class,
                        FilePath = consumerPath,
                        Dependencies = ["ReviewProcessor"]
                    }
                ];

                File.WriteAllText(reviewedOutputFile, JsonSerializer.Serialize(reviewedSymbols));
                File.WriteAllText(consumerOutputFile, JsonSerializer.Serialize(consumerSymbols));

                IManifestEntry reviewedEntry = new ManifestEntryStub
                {
                    OutputFile = reviewedOutputFile,
                    SourcePath = reviewedPath
                };

                IManifestEntry consumerEntry = new ManifestEntryStub
                {
                    OutputFile = consumerOutputFile,
                    SourcePath = consumerPath
                };

                FakeManifestManager manifestManager = new(reviewedEntry, [reviewedEntry, consumerEntry]);

                SymbolContextProvider provider = new(manifestManager);

                ReviewContextRequest request = new()
                {
                    RepositoryId = 1,
                    RepositoryName = "Demo Repo",
                    RepoFileId = 1,
                    Language = "CSharp",
                    ContextKey = Consts.ContextKeyFiles,
                    MaxItems = 10,
                    SourcePath = reviewedPath
                };

                // Act
                ReviewContextResponse response = await provider.GetContextAsync(request, TestContext.CancellationToken);

                // Assert
                Assert.AreEqual(Consts.ContextKeyFiles, response.ContextKey);
                Assert.HasCount(1, response.Items);
                Assert.AreEqual("Demo.ReviewWorkerService", response.Items[0].Title);
                Assert.AreEqual(consumerPath, response.Items[0].SourcePath);
                Assert.Contains("references", response.Items[0].Content);
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
            private readonly Queue<StubResponse> _responses;
            private readonly List<string> _requestBodies = [];

            public StubHttpMessageHandler(HttpStatusCode statusCode, string body, string mediaType)
                : this([new StubResponse(statusCode, body, mediaType)])
            {
            }

            public StubHttpMessageHandler(IEnumerable<StubResponse> responses)
            {
                _responses = new Queue<StubResponse>(responses);
            }

            public int RequestCount
            {
                get { return _requestBodies.Count; }
            }

            public IReadOnlyList<string> RequestBodies
            {
                get { return _requestBodies; }
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _requestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("No stub responses configured.");
                }

                StubResponse next = _responses.Count == 1 ? _responses.Peek() : _responses.Dequeue();

                HttpResponseMessage response = new(next.StatusCode)
                {
                    Content = new StringContent(next.Body, Encoding.UTF8, next.MediaType)
                };

                return response;
            }
        }

        private sealed class StubResponse
        {
            public StubResponse(HttpStatusCode statusCode, string body, string mediaType)
            {
                StatusCode = statusCode;
                Body = body;
                MediaType = mediaType;
            }

            public HttpStatusCode StatusCode { get; }

            public string Body { get; }

            public string MediaType { get; }
        }

        private sealed class FakeOperationalDiagnostics : IOperationalDiagnostics
        {
            public void Log(
                OperationalLogLevel level,
                string component,
                string operation,
                string message,
                Exception exception = null,
                TimeSpan? duration = null,
                int? retryAttempt = null)
            {
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
            private readonly IReadOnlyList<IManifestEntry> _entries;

            public FakeManifestManager(IManifestEntry entry)
                : this(entry, [entry])
            {
            }

            public FakeManifestManager(IManifestEntry entry, IReadOnlyList<IManifestEntry> entries)
            {
                _entry = entry;
                _entries = entries;
            }

            public bool RequiresAnalysis(string repositoryName, string filePath, int currentAnalyserVersion)
            {
                return false;
            }

            public void RecordAnalysis(string repositoryName, IManifestEntry entry)
            {
            }

            public IManifestEntry GetEntry(string repositoryName, string filePath)
            {
                return string.Equals(filePath, _entry.SourcePath, StringComparison.OrdinalIgnoreCase) ? _entry : null;
            }

            public IReadOnlyList<IManifestEntry> GetEntries(string repositoryName)
            {
                return _entries;
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

        public TestContext TestContext { get; set; }
    }
}
