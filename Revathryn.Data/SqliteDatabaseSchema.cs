using Revathryn.Contracts;

namespace Revathryn.Data
{
    /// <summary>DDL statements for the SQLite schema.</summary>
    internal static class SqliteDatabaseSchema
    {
        /// <summary>Creates the Repositories table.</summary>
        internal const string CreateRepositories = """
            CREATE TABLE IF NOT EXISTS Repositories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                LocalPath TEXT NOT NULL,
                RepositoryUri TEXT NOT NULL DEFAULT '',
                Enabled INTEGER NOT NULL DEFAULT 0,
                AutomaticReviewEnabled INTEGER NOT NULL DEFAULT 0,
                MaxContextRequests INTEGER NOT NULL DEFAULT 0,
                AIEngineId INTEGER NULL,
                ScanOptions TEXT NOT NULL DEFAULT '',
                LastSuccessfulScanDate TEXT NULL,
                CreatedDate TEXT NOT NULL,
                LastModifiedDate TEXT NOT NULL
            );
            """;

        /// <summary>Creates the RepoFiles table.</summary>
        internal const string CreateRepoFiles = """
            CREATE TABLE IF NOT EXISTS RepoFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RelativePath TEXT NOT NULL,
                FileName TEXT NOT NULL,
                Extension TEXT NOT NULL DEFAULT '',
                LastModifiedDate TEXT NOT NULL,
                Sha256 TEXT NOT NULL,
                FileSize INTEGER NOT NULL DEFAULT 0,
                IsExcluded INTEGER NOT NULL DEFAULT 0,
                IsRemoved INTEGER NOT NULL DEFAULT 0,
                ProjectPath TEXT NOT NULL DEFAULT '',
                LastDiscoveredDate TEXT NOT NULL,
                CONSTRAINT UQ_RepoFiles_Repository_RelativePath UNIQUE (RepositoryId, RelativePath),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the ReviewTypes table.</summary>
        internal const string CreateReviewTypes = """
            CREATE TABLE IF NOT EXISTS ReviewTypes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                Key TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                FileExtensions TEXT NOT NULL DEFAULT '',
                Prompt TEXT NOT NULL DEFAULT '',
                DefaultPriority INTEGER NOT NULL DEFAULT 0,
                ReviewDefinitionVersion TEXT NOT NULL DEFAULT '',
                Enabled INTEGER NOT NULL DEFAULT 1,
                CreatedDate TEXT NOT NULL,
                LastModifiedDate TEXT NOT NULL,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the ReviewLinks table.</summary>
        internal const string CreateReviewLinks = """
            CREATE TABLE IF NOT EXISTS ReviewLinks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepoFileId INTEGER NOT NULL,
                ReviewTypeId INTEGER NOT NULL,
                Enabled INTEGER NOT NULL DEFAULT 1,
                PriorityOverride INTEGER NULL,
                CONSTRAINT UQ_ReviewLinks_File_ReviewType UNIQUE (RepoFileId, ReviewTypeId),
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the RepoFileAnalysis table.</summary>
        internal const string CreateRepoFileAnalysis = """
            CREATE TABLE IF NOT EXISTS RepoFileAnalysis (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NOT NULL,
                ReviewTypeId INTEGER NOT NULL,
                Sha256 TEXT NOT NULL,
                LastAnalysedDate TEXT NOT NULL,
                AnalysisVersion TEXT NOT NULL DEFAULT '',
                ReviewDefinitionVersion TEXT NOT NULL DEFAULT '',
                CONSTRAINT UQ_RepoFileAnalysis_File_ReviewType UNIQUE (RepoFileId, ReviewTypeId),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the transient Queue table.</summary>
        internal const string CreateQueue = """
            CREATE TABLE IF NOT EXISTS Queue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NOT NULL,
                ReviewTypeId INTEGER NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 0,
                CreatedDate TEXT NOT NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                MaxAttempts INTEGER NOT NULL DEFAULT 0,
                NextAttemptUtc TEXT NULL,
                LastError TEXT NOT NULL DEFAULT '',
                LeasedUntilUtc TEXT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT UQ_Queue_File_ReviewType UNIQUE (RepoFileId, ReviewTypeId),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the Findings table.</summary>
        internal const string CreateFindings = """
            CREATE TABLE IF NOT EXISTS Findings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NULL,
                ReviewTypeId INTEGER NULL,
                Fingerprint TEXT NOT NULL,
                Severity INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                SuggestedFix TEXT NOT NULL DEFAULT '',
                Status INTEGER NOT NULL,
                Category INTEGER NOT NULL,
                CreatedDate TEXT NOT NULL,
                LastModifiedDate TEXT NOT NULL,
                DuplicateOfFindingId INTEGER NULL,
                DuplicateSimilarityScore REAL NULL,
                FindingKey TEXT NOT NULL DEFAULT '',
                LineStart INTEGER NULL,
                LineEnd INTEGER NULL,
                ResolutionOrigin INTEGER NOT NULL DEFAULT 0,
                ResolvedUtc TEXT NULL,
                ResolvedReviewSha256 TEXT NOT NULL DEFAULT '',
                CONSTRAINT UQ_Findings_Repository_Fingerprint UNIQUE (RepositoryId, Fingerprint),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE SET NULL,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the DuplicateDetectionQueue table.</summary>
        internal const string CreateDuplicateDetectionQueue = """
            CREATE TABLE IF NOT EXISTS DuplicateDetectionQueue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                FindingId INTEGER NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                CreatedUtc TEXT NOT NULL,
                NextAttemptUtc TEXT NULL,
                LeasedUntilUtc TEXT NULL,
                LastError TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (FindingId) REFERENCES Findings (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the AIEngines table.</summary>
        internal static readonly string CreateAIEngines = $"""
            CREATE TABLE IF NOT EXISTS AIEngines (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Provider INTEGER NOT NULL,
                Endpoint TEXT NOT NULL DEFAULT '',
                Model TEXT NOT NULL DEFAULT '',
                SecretKeyName TEXT NOT NULL DEFAULT '',
                MaxTokens INTEGER NOT NULL DEFAULT {Consts.DefaultAIMaxTokens},
                Enabled INTEGER NOT NULL DEFAULT 1,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                UseArchitecturePass INTEGER NOT NULL DEFAULT 0
            );
            """;

        /// <summary>Creates the AISettings singleton table.</summary>
        internal static readonly string CreateAISettings = $"""
            CREATE TABLE IF NOT EXISTS AISettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                MasterPrompt TEXT NOT NULL DEFAULT '',
                LastModifiedDate TEXT NOT NULL,
                IssueSimilarityEnabled INTEGER NOT NULL DEFAULT 1,
                IssueSimilarityThreshold REAL NOT NULL DEFAULT {Consts.DefaultIssueSimilarityThreshold},
                IssueSimilarityMinimumTokenLength INTEGER NOT NULL DEFAULT {Consts.DefaultIssueSimilarityMinimumTokenLength},
                IssueDefaultPageSize INTEGER NOT NULL DEFAULT {Consts.IssueDefaultPageSize},
                IssuePageSizeOptions TEXT NOT NULL DEFAULT ''
            );
            """;

        /// <summary>Creates the SeverityRules table holding configurable severity overrides.</summary>
        internal const string CreateSeverityRules = """
            CREATE TABLE IF NOT EXISTS SeverityRules (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RuleOrder INTEGER NOT NULL DEFAULT 0,
                Name TEXT NOT NULL,
                MatchText TEXT NOT NULL,
                MatchField INTEGER NOT NULL DEFAULT 2,
                MatchSeverity INTEGER NULL,
                Action INTEGER NOT NULL DEFAULT 1,
                TargetSeverity INTEGER NOT NULL DEFAULT 2,
                Enabled INTEGER NOT NULL DEFAULT 1,
                CreatedDate TEXT NOT NULL,
                LastModifiedDate TEXT NOT NULL
            );
            """;

        /// <summary>Creates the RepoProjects table.</summary>
        internal const string CreateRepoProjects = """
            CREATE TABLE IF NOT EXISTS RepoProjects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                Kind INTEGER NOT NULL DEFAULT 0,
                RelativePath TEXT NOT NULL,
                DirectoryPath TEXT NOT NULL DEFAULT '',
                SolutionPath TEXT NOT NULL DEFAULT '',
                SolutionId INTEGER NULL,
                CONSTRAINT UQ_RepoProjects_Repository_RelativePath UNIQUE (RepositoryId, RelativePath),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (SolutionId) REFERENCES RepoProjects (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the RepositoryScans table.</summary>
        internal const string CreateRepositoryScans = """
            CREATE TABLE IF NOT EXISTS RepositoryScans (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                StartedUtc TEXT NOT NULL,
                CompletedUtc TEXT NOT NULL,
                Succeeded INTEGER NOT NULL DEFAULT 0,
                FilesDiscovered INTEGER NOT NULL DEFAULT 0,
                NewFiles INTEGER NOT NULL DEFAULT 0,
                ChangedFiles INTEGER NOT NULL DEFAULT 0,
                UnchangedFiles INTEGER NOT NULL DEFAULT 0,
                RemovedFiles INTEGER NOT NULL DEFAULT 0,
                RenamedFiles INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the ReviewUsage table.</summary>
        internal const string CreateReviewUsage = """
            CREATE TABLE IF NOT EXISTS ReviewUsage (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NOT NULL,
                ReviewTypeId INTEGER NOT NULL,
                EngineId INTEGER NOT NULL,
                EngineName TEXT NOT NULL DEFAULT '',
                EngineModel TEXT NOT NULL DEFAULT '',
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                PromptTokens INTEGER NOT NULL DEFAULT 0,
                CompletionTokens INTEGER NOT NULL DEFAULT 0,
                TotalTokens INTEGER NOT NULL DEFAULT 0,
                CachedTokens INTEGER NOT NULL DEFAULT 0,
                OccurredUtc TEXT NOT NULL,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the ReviewContexts table.</summary>
        internal const string CreateReviewContexts = """
            CREATE TABLE IF NOT EXISTS ReviewContexts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NOT NULL,
                ReviewTypeId INTEGER NOT NULL,
                Sha256 TEXT NOT NULL,
                Language TEXT NOT NULL DEFAULT '',
                FilePath TEXT NOT NULL DEFAULT '',
                ReviewInstructions TEXT NOT NULL DEFAULT '',
                FileContent TEXT NOT NULL DEFAULT '',
                ContextItemsJson TEXT NOT NULL DEFAULT '',
                CreatedUtc TEXT NOT NULL,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the ReviewEngineSettings singleton table holding the independent review engine configuration.</summary>
        internal const string CreateReviewEngineSettings = """
            CREATE TABLE IF NOT EXISTS ReviewEngineSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Provider INTEGER NOT NULL DEFAULT 0,
                Endpoint TEXT NOT NULL DEFAULT '',
                Model TEXT NOT NULL DEFAULT '',
                Temperature REAL NULL,
                ReasoningEffort TEXT NOT NULL DEFAULT '',
                MaxTokens INTEGER NOT NULL DEFAULT 0,
                SecretKeyName TEXT NOT NULL DEFAULT '',
                Enabled INTEGER NOT NULL DEFAULT 0,
                AdditionalOptions TEXT NOT NULL DEFAULT '',
                LastModifiedUtc TEXT NOT NULL
            );
            """;

        /// <summary>Creates the IssueValidations table. Each row is an immutable historical validation of a finding.</summary>
        internal const string CreateIssueValidations = """
            CREATE TABLE IF NOT EXISTS IssueValidations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FindingId INTEGER NOT NULL,
                RepositoryId INTEGER NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                RetryOfValidationId INTEGER NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                CreatedUtc TEXT NOT NULL,
                StartedUtc TEXT NULL,
                CompletedUtc TEXT NULL,
                NextAttemptUtc TEXT NULL,
                LeasedUntilUtc TEXT NULL,
                Provider INTEGER NOT NULL DEFAULT 0,
                Model TEXT NOT NULL DEFAULT '',
                EngineConfigSnapshotJson TEXT NOT NULL DEFAULT '',
                PromptVersion TEXT NOT NULL DEFAULT '',
                SystemPrompt TEXT NOT NULL DEFAULT '',
                UserPrompt TEXT NOT NULL DEFAULT '',
                ContextReferenceJson TEXT NOT NULL DEFAULT '',
                OriginalSeverity INTEGER NOT NULL DEFAULT 0,
                OriginalTitle TEXT NOT NULL DEFAULT '',
                OriginalDescription TEXT NOT NULL DEFAULT '',
                OriginalSuggestedFix TEXT NOT NULL DEFAULT '',
                Verdict INTEGER NULL,
                Confidence REAL NULL,
                Summary TEXT NOT NULL DEFAULT '',
                Reasoning TEXT NOT NULL DEFAULT '',
                RecommendedSeverity INTEGER NULL,
                SeverityReasoning TEXT NOT NULL DEFAULT '',
                EvidenceJson TEXT NOT NULL DEFAULT '',
                SuggestionsJson TEXT NOT NULL DEFAULT '',
                LimitationsJson TEXT NOT NULL DEFAULT '',
                RawResponse TEXT NOT NULL DEFAULT '',
                ErrorMessage TEXT NOT NULL DEFAULT '',
                FailureClassification INTEGER NULL,
                PromptTokens INTEGER NOT NULL DEFAULT 0,
                CompletionTokens INTEGER NOT NULL DEFAULT 0,
                TotalTokens INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (FindingId) REFERENCES Findings (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the Prompts table holding logical prompt identities.</summary>
        internal const string CreatePrompts = """
            CREATE TABLE IF NOT EXISTS Prompts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PromptType INTEGER NOT NULL,
                ReviewTypeId INTEGER NULL,
                CreatedUtc TEXT NOT NULL,
                LastModifiedUtc TEXT NOT NULL,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the immutable PromptVersions table.</summary>
        internal const string CreatePromptVersions = """
            CREATE TABLE IF NOT EXISTS PromptVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PromptId INTEGER NOT NULL,
                VersionNumber INTEGER NOT NULL,
                PromptText TEXT NOT NULL DEFAULT '',
                Description TEXT NOT NULL DEFAULT '',
                Origin INTEGER NOT NULL DEFAULT 0,
                SourceOptimisationId INTEGER NULL,
                CreatedUtc TEXT NOT NULL,
                CreatedBy TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 0,
                IsDraft INTEGER NOT NULL DEFAULT 0,
                ActivatedUtc TEXT NULL,
                FOREIGN KEY (PromptId) REFERENCES Prompts (Id) ON DELETE CASCADE,
                FOREIGN KEY (SourceOptimisationId) REFERENCES PromptOptimisations (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the PromptOptimisations table holding disagreement analysis and generated recommendations.</summary>
        internal const string CreatePromptOptimisations = """
            CREATE TABLE IF NOT EXISTS PromptOptimisations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                IssueValidationId INTEGER NULL,
                FindingId INTEGER NOT NULL,
                RepositoryId INTEGER NOT NULL,
                ReviewTypeId INTEGER NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                RetryOfOptimisationId INTEGER NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                CreatedUtc TEXT NOT NULL,
                StartedUtc TEXT NULL,
                CompletedUtc TEXT NULL,
                NextAttemptUtc TEXT NULL,
                LeasedUntilUtc TEXT NULL,
                Provider INTEGER NOT NULL DEFAULT 0,
                Model TEXT NOT NULL DEFAULT '',
                EngineConfigSnapshotJson TEXT NOT NULL DEFAULT '',
                OriginalSystemPrompt TEXT NOT NULL DEFAULT '',
                OriginalReviewPrompt TEXT NOT NULL DEFAULT '',
                OriginalSystemPromptVersionId INTEGER NULL,
                OriginalReviewPromptVersionId INTEGER NULL,
                AnalysisJson TEXT NOT NULL DEFAULT '',
                SystemPromptRecommendationJson TEXT NOT NULL DEFAULT '',
                ReviewPromptRecommendationJson TEXT NOT NULL DEFAULT '',
                RawResponse TEXT NOT NULL DEFAULT '',
                ErrorMessage TEXT NOT NULL DEFAULT '',
                FailureClassification INTEGER NULL,
                PromptTokens INTEGER NOT NULL DEFAULT 0,
                CompletionTokens INTEGER NOT NULL DEFAULT 0,
                TotalTokens INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (IssueValidationId) REFERENCES IssueValidations (Id) ON DELETE SET NULL,
                FOREIGN KEY (FindingId) REFERENCES Findings (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the PromptTestRuns table holding isolated prompt test executions.</summary>
        internal const string CreatePromptTestRuns = """
            CREATE TABLE IF NOT EXISTS PromptTestRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PromptVersionId INTEGER NOT NULL,
                ReviewTypeId INTEGER NULL,
                OriginalIssueId INTEGER NULL,
                DetectionModel TEXT NOT NULL DEFAULT '',
                ReviewContextId INTEGER NULL,
                ContextSnapshotJson TEXT NOT NULL DEFAULT '',
                PromptText TEXT NOT NULL DEFAULT '',
                OriginalOutputJson TEXT NOT NULL DEFAULT '',
                TestOutputJson TEXT NOT NULL DEFAULT '',
                ComparisonJson TEXT NOT NULL DEFAULT '',
                Status INTEGER NOT NULL DEFAULT 0,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                CreatedUtc TEXT NOT NULL,
                StartedUtc TEXT NULL,
                CompletedUtc TEXT NULL,
                NextAttemptUtc TEXT NULL,
                LeasedUntilUtc TEXT NULL,
                Provider INTEGER NOT NULL DEFAULT 0,
                Model TEXT NOT NULL DEFAULT '',
                EngineConfigSnapshotJson TEXT NOT NULL DEFAULT '',
                ErrorMessage TEXT NOT NULL DEFAULT '',
                FailureClassification INTEGER NULL,
                PromptTokens INTEGER NOT NULL DEFAULT 0,
                CompletionTokens INTEGER NOT NULL DEFAULT 0,
                TotalTokens INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (PromptVersionId) REFERENCES PromptVersions (Id) ON DELETE CASCADE,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE SET NULL,
                FOREIGN KEY (OriginalIssueId) REFERENCES Findings (Id) ON DELETE SET NULL,
                FOREIGN KEY (ReviewContextId) REFERENCES ReviewContexts (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the cached per-file architecture summary table.</summary>
        internal const string CreateRepoFileArchitectures = """
            CREATE TABLE IF NOT EXISTS RepoFileArchitectures (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NOT NULL,
                Sha256 TEXT NOT NULL,
                Summary TEXT NOT NULL DEFAULT '',
                EngineName TEXT NOT NULL DEFAULT '',
                EngineModel TEXT NOT NULL DEFAULT '',
                AnalysisVersion TEXT NOT NULL DEFAULT '',
                CreatedUtc TEXT NOT NULL,
                CONSTRAINT UQ_RepoFileArchitectures_RepoFile UNIQUE (RepoFileId),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE CASCADE
            );
            """;

        /// <summary>Creates the immutable architecture pass failure table.</summary>
        internal const string CreateArchitectureAnalysisFailures = """
            CREATE TABLE IF NOT EXISTS ArchitectureAnalysisFailures (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryId INTEGER NOT NULL,
                RepoFileId INTEGER NULL,
                FilePath TEXT NOT NULL DEFAULT '',
                Language TEXT NOT NULL DEFAULT '',
                Sha256 TEXT NOT NULL DEFAULT '',
                SystemPrompt TEXT NOT NULL DEFAULT '',
                FileContent TEXT NOT NULL DEFAULT '',
                Error TEXT NOT NULL DEFAULT '',
                RawResponse TEXT NOT NULL DEFAULT '',
                EngineName TEXT NOT NULL DEFAULT '',
                EngineModel TEXT NOT NULL DEFAULT '',
                CreatedUtc TEXT NOT NULL,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE SET NULL
            );
            """;

        /// <summary>Creates the non-unique query indexes.</summary>
        internal static readonly string[] CreateIndexes =
        [
            "CREATE INDEX IF NOT EXISTS IX_RepoFiles_RepositoryId ON RepoFiles (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewTypes_RepositoryId ON ReviewTypes (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewLinks_RepoFileId ON ReviewLinks (RepoFileId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewLinks_ReviewTypeId ON ReviewLinks (ReviewTypeId);",
            "CREATE INDEX IF NOT EXISTS IX_RepoFileAnalysis_RepositoryId ON RepoFileAnalysis (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_RepoFileAnalysis_RepoFileId ON RepoFileAnalysis (RepoFileId);",
            "CREATE INDEX IF NOT EXISTS IX_RepoFileAnalysis_ReviewTypeId ON RepoFileAnalysis (ReviewTypeId);",
            "CREATE INDEX IF NOT EXISTS IX_Queue_RepositoryId ON Queue (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_Queue_Priority ON Queue (Priority DESC, CreatedDate ASC);",
            "CREATE INDEX IF NOT EXISTS IX_Queue_Status ON Queue (Status, Priority DESC, CreatedDate ASC);",
            "CREATE INDEX IF NOT EXISTS IX_Queue_LeasedUntilUtc ON Queue (LeasedUntilUtc);",
            "CREATE INDEX IF NOT EXISTS IX_Findings_RepositoryId ON Findings (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_Findings_RepoFileId ON Findings (RepoFileId);",
            "CREATE INDEX IF NOT EXISTS IX_Findings_ReviewTypeId ON Findings (ReviewTypeId);",
            "CREATE INDEX IF NOT EXISTS IX_Findings_Status ON Findings (Status);",
            "CREATE INDEX IF NOT EXISTS IX_Findings_DuplicateOfFindingId ON Findings (DuplicateOfFindingId);",
            "CREATE INDEX IF NOT EXISTS IX_DuplicateDetectionQueue_Status ON DuplicateDetectionQueue (Status, NextAttemptUtc);",
            "CREATE INDEX IF NOT EXISTS IX_DuplicateDetectionQueue_FindingId ON DuplicateDetectionQueue (FindingId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_DuplicateDetectionQueue_ActiveFinding ON DuplicateDetectionQueue (FindingId) WHERE Status IN (0, 1);",
            "CREATE INDEX IF NOT EXISTS IX_RepoProjects_RepositoryId ON RepoProjects (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_RepositoryScans_RepositoryId ON RepositoryScans (RepositoryId, StartedUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewUsage_RepositoryId ON ReviewUsage (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewUsage_OccurredUtc ON ReviewUsage (OccurredUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewContexts_RepositoryId ON ReviewContexts (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewContexts_RepoFileId ON ReviewContexts (RepoFileId);",
            "CREATE INDEX IF NOT EXISTS IX_IssueValidations_FindingId ON IssueValidations (FindingId, CreatedUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_IssueValidations_Status ON IssueValidations (Status, NextAttemptUtc);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_IssueValidations_ActiveFinding ON IssueValidations (FindingId) WHERE Status IN (0, 1);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_ReviewTypes_Repository_Key ON ReviewTypes (RepositoryId, Key) WHERE Key <> '';",
            "CREATE INDEX IF NOT EXISTS IX_Prompts_ReviewTypeId ON Prompts (ReviewTypeId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_Prompts_System ON Prompts (PromptType) WHERE PromptType = 0 AND ReviewTypeId IS NULL;",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_Prompts_ReviewType ON Prompts (ReviewTypeId) WHERE PromptType = 1 AND ReviewTypeId IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS IX_PromptVersions_PromptId ON PromptVersions (PromptId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_PromptVersions_Prompt_Version ON PromptVersions (PromptId, VersionNumber);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_PromptVersions_Active ON PromptVersions (PromptId) WHERE IsActive = 1;",
            "CREATE INDEX IF NOT EXISTS IX_PromptOptimisations_FindingId ON PromptOptimisations (FindingId, CreatedUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_PromptOptimisations_Status ON PromptOptimisations (Status, NextAttemptUtc);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_PromptOptimisations_ActiveValidation ON PromptOptimisations (IssueValidationId) WHERE Status IN (0, 1) AND IssueValidationId IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS IX_PromptTestRuns_PromptVersionId ON PromptTestRuns (PromptVersionId, CreatedUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_PromptTestRuns_Status ON PromptTestRuns (Status, NextAttemptUtc);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_PromptTestRuns_ActiveVersionIssue ON PromptTestRuns (PromptVersionId, OriginalIssueId) WHERE Status IN (0, 1) AND OriginalIssueId IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS IX_SeverityRules_Order ON SeverityRules (RuleOrder, Id);",
            "CREATE INDEX IF NOT EXISTS IX_RepoFileArchitectures_RepositoryId ON RepoFileArchitectures (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_RepoFileArchitectures_Sha256 ON RepoFileArchitectures (Sha256);",
            "CREATE INDEX IF NOT EXISTS IX_ArchitectureAnalysisFailures_RepositoryId ON ArchitectureAnalysisFailures (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ArchitectureAnalysisFailures_CreatedUtc ON ArchitectureAnalysisFailures (CreatedUtc DESC);"
        ];
    }
}
