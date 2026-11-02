using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Data;

namespace Revathryn.Engine.Tests
{
    /// <summary>Real SQLite persistence in a temporary directory, used so validation workflow tests exercise genuine repositories while AI is mocked.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class ValidationTestFixture : IDisposable
    {
        public const string SourcePath = "src/Example.cs";

        private readonly string _directory;

        public ValidationTestFixture()
        {
            _directory = Path.Combine(Path.GetTempPath(), "revathryn-engine-tests", Guid.NewGuid().ToString("N"));
            RepositoryPath = Path.Combine(_directory, "repo");
            _ = Directory.CreateDirectory(Path.Combine(RepositoryPath, "src"));

            DatabaseOptions options = new(new RevathrynSettings("test")) { ConnectionString = $"Data Source={Path.Combine(_directory, "test.db")};Pooling=False" };
            SqliteConnectionFactory factory = new(options);
            new SqliteDatabaseInitializer(factory).Initialize();

            Repositories = new RepositoryRepository(factory);
            Files = new RepoFileRepository(factory);
            ReviewTypes = new ReviewTypeRepository(factory);
            Contexts = new ReviewContextRepository(factory);
            Findings = new FindingRepository(factory);
            Validations = new IssueValidationRepository(factory);
            Settings = new ReviewEngineSettingsRepository(factory);
        }

        public string RepositoryPath { get; }

        public long RepositoryId { get; private set; }

        public long FileId { get; private set; }

        public long ReviewTypeId { get; private set; }

        public RepositoryRepository Repositories { get; }

        public RepoFileRepository Files { get; }

        public ReviewTypeRepository ReviewTypes { get; }

        public ReviewContextRepository Contexts { get; }

        public FindingRepository Findings { get; }

        public IssueValidationRepository Validations { get; }

        public ReviewEngineSettingsRepository Settings { get; }

        public async Task InitialiseAsync(string sourceContent, string fileSha256, CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;

            RepositoryId = await Repositories.AddAsync(
                new Repository { Name = "Repo", LocalPath = RepositoryPath, Enabled = true, CreatedDate = now, LastModifiedDate = now },
                cancellationToken);

            if (sourceContent is not null)
            {
                await File.WriteAllTextAsync(Path.Combine(RepositoryPath, "src", "Example.cs"), sourceContent, cancellationToken);
            }

            FileId = await Files.UpsertAsync(
                new RepoFile
                {
                    RepositoryId = RepositoryId,
                    RelativePath = SourcePath,
                    FileName = "Example.cs",
                    Extension = "cs",
                    Sha256 = fileSha256,
                    LastModifiedDate = now,
                    LastDiscoveredDate = now
                },
                cancellationToken);

            ReviewTypeId = await ReviewTypes.AddAsync(
                new ReviewType
                {
                    RepositoryId = RepositoryId,
                    Name = "C# Review",
                    FileExtensions = "cs",
                    DefaultPriority = 10,
                    Enabled = true,
                    CreatedDate = now,
                    LastModifiedDate = now
                },
                cancellationToken);
        }

        public async Task<long> AddContextAsync(AIReviewContext context, CancellationToken cancellationToken)
        {
            context.RepositoryId = RepositoryId;
            context.RepoFileId = FileId;
            context.ReviewTypeId = ReviewTypeId;
            context.CreatedUtc = DateTime.UtcNow;
            return await Contexts.AddAsync(context, cancellationToken);
        }

        public async Task<long> AddFindingAsync(long? reviewContextId, CancellationToken cancellationToken, FindingCategory category = FindingCategory.CodeIssue, string fingerprint = "fp-1")
        {
            DateTime now = DateTime.UtcNow;

            return await Findings.AddAsync(
                new Finding
                {
                    RepositoryId = RepositoryId,
                    RepoFileId = FileId,
                    ReviewTypeId = ReviewTypeId,
                    ReviewContextId = reviewContextId,
                    Fingerprint = fingerprint,
                    Severity = FindingSeverity.High,
                    Title = "Potential missing null check",
                    Description = "The value can be null when it is dereferenced.",
                    SuggestedFix = "Add a null guard.",
                    Status = FindingStatus.New,
                    Category = category,
                    CreatedDate = now,
                    LastModifiedDate = now
                },
                cancellationToken);
        }

        public async Task SaveEnabledSettingsAsync(CancellationToken cancellationToken, bool enabled = true)
        {
            await Settings.SaveAsync(
                new ReviewEngineSettings
                {
                    Provider = AIProvider.OpenAI,
                    Endpoint = "https://review.test/v1",
                    Model = "review-model",
                    Temperature = 0.1,
                    ReasoningEffort = "high",
                    MaxTokens = 4096,
                    SecretKeyName = "REVIEW_KEY",
                    Enabled = enabled,
                    LastModifiedUtc = DateTime.UtcNow
                },
                cancellationToken);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }
    }
}
