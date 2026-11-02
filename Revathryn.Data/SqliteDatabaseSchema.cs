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
                CONSTRAINT UQ_Findings_Repository_Fingerprint UNIQUE (RepositoryId, Fingerprint),
                FOREIGN KEY (RepositoryId) REFERENCES Repositories (Id) ON DELETE CASCADE,
                FOREIGN KEY (RepoFileId) REFERENCES RepoFiles (Id) ON DELETE SET NULL,
                FOREIGN KEY (ReviewTypeId) REFERENCES ReviewTypes (Id) ON DELETE SET NULL
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
                IsDefault INTEGER NOT NULL DEFAULT 0
            );
            """;

        /// <summary>Creates the AISettings singleton table.</summary>
        internal const string CreateAISettings = """
            CREATE TABLE IF NOT EXISTS AISettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                MasterPrompt TEXT NOT NULL DEFAULT '',
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
            "CREATE INDEX IF NOT EXISTS IX_RepoProjects_RepositoryId ON RepoProjects (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_RepositoryScans_RepositoryId ON RepositoryScans (RepositoryId, StartedUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewUsage_RepositoryId ON ReviewUsage (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewUsage_OccurredUtc ON ReviewUsage (OccurredUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewContexts_RepositoryId ON ReviewContexts (RepositoryId);",
            "CREATE INDEX IF NOT EXISTS IX_ReviewContexts_RepoFileId ON ReviewContexts (RepoFileId);",
            "CREATE UNIQUE INDEX IF NOT EXISTS UQ_ReviewTypes_Repository_Key ON ReviewTypes (RepositoryId, Key) WHERE Key <> '';"
        ];
    }
}
