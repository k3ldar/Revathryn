using Microsoft.Data.Sqlite;

using Revathryn.Contracts;
using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Data
{
    /// <summary>Creates the SQLite database and applies the schema.</summary>
    public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        /// <summary>Initialises a new initializer over the supplied connection factory.</summary>
        public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>Creates the database and applies pending schema migrations.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            ApplySchema(connection);
        }

        /// <summary>Synchronous counterpart of <see cref="InitializeAsync"/>.</summary>
        public void Initialize()
        {
            using SqliteConnection connection = _connectionFactory.Open();
            ApplySchema(connection);
        }

        private static void ApplySchema(SqliteConnection connection)
        {
            RebuildLegacyReviewSchema(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = string.Join(
                Environment.NewLine,
                SqliteDatabaseSchema.CreateRepositories,
                SqliteDatabaseSchema.CreateRepoFiles,
                SqliteDatabaseSchema.CreateReviewTypes,
                SqliteDatabaseSchema.CreateRepositoryReviews,
                SqliteDatabaseSchema.CreateReviewLinks,
                SqliteDatabaseSchema.CreateRepoFileAnalysis,
                SqliteDatabaseSchema.CreateQueue,
                SqliteDatabaseSchema.CreateFindings,
                SqliteDatabaseSchema.CreateAIEngines,
                SqliteDatabaseSchema.CreateAISettings,
                SqliteDatabaseSchema.CreateSeverityRules,
                SqliteDatabaseSchema.CreateRepoProjects,
                SqliteDatabaseSchema.CreateRepositoryScans,
                SqliteDatabaseSchema.CreateReviewUsage,
                SqliteDatabaseSchema.CreateReviewContexts,
                SqliteDatabaseSchema.CreateReviewEngineSettings,
                SqliteDatabaseSchema.CreateIssueValidations,
                SqliteDatabaseSchema.CreateDuplicateDetectionQueue,
                SqliteDatabaseSchema.CreatePrompts,
                SqliteDatabaseSchema.CreatePromptOptimisations,
                SqliteDatabaseSchema.CreatePromptVersions,
                SqliteDatabaseSchema.CreatePromptTestRuns,
                SqliteDatabaseSchema.CreateRepoFileArchitectures,
                SqliteDatabaseSchema.CreateArchitectureAnalysisFailures);

            command.ExecuteNonQuery();

            ApplyColumnMigrations(connection);

            foreach (string indexStatement in SqliteDatabaseSchema.CreateIndexes)
            {
                command.CommandText = indexStatement;
                command.ExecuteNonQuery();
            }
        }

        private static void ApplyColumnMigrations(SqliteConnection connection)
        {
            EnsureColumn(connection, "ReviewTypes", "ReviewDefinitionVersion", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewTypes", "Key", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "RepoFileAnalysis", "ReviewDefinitionVersion", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "RepoFileAnalysis", "Branch", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Repositories", "ScanOptions", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Repositories", "LastSuccessfulScanDate", "TEXT NULL");
            EnsureColumn(connection, "Repositories", "AIEngineId", "INTEGER NULL");
            EnsureColumn(connection, "Repositories", "AutoReviewMinimumSeverity", "INTEGER NOT NULL DEFAULT " + Consts.DefaultAutoReviewMinimumSeverity);
            EnsureColumn(connection, "Repositories", "AutoReviewConfidenceThreshold", "INTEGER NOT NULL DEFAULT " + Consts.DefaultAutoReviewConfidenceThreshold);
            EnsureColumn(connection, "RepoFiles", "IsRemoved", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "RepoFiles", "ProjectPath", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "RepositoryScans", "RenamedFiles", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "RepositoryScans", "Branch", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Queue", "AttemptCount", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "Queue", "MaxAttempts", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "Queue", "NextAttemptUtc", "TEXT NULL");
            EnsureColumn(connection, "Queue", "LastError", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Queue", "LeasedUntilUtc", "TEXT NULL");
            EnsureColumn(connection, "Queue", "Status", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "Queue", "Branch", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Findings", "ReviewContextId", "INTEGER NULL");
            EnsureColumn(connection, "Findings", "DuplicateOfFindingId", "INTEGER NULL");
            EnsureColumn(connection, "Findings", "DuplicateSimilarityScore", "REAL NULL");
            EnsureColumn(connection, "Findings", "FindingKey", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Findings", "LineStart", "INTEGER NULL");
            EnsureColumn(connection, "Findings", "LineEnd", "INTEGER NULL");
            EnsureColumn(connection, "Findings", "ResolutionOrigin", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "Findings", "ResolvedUtc", "TEXT NULL");
            EnsureColumn(connection, "Findings", "ResolvedReviewSha256", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Findings", "EngineModel", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Findings", "Branch", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "AIEngines", "MaxTokens", "INTEGER NOT NULL DEFAULT " + Consts.DefaultAIMaxTokens);
            EnsureColumn(connection, "AIEngines", "UseArchitecturePass", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "AIEngines", "MaxSimultaneousConnections", "INTEGER NOT NULL DEFAULT " + Consts.DefaultAIMaxSimultaneousConnections);
            EnsureColumn(connection, "ReviewEngineSettings", "MaxSimultaneousConnections", "INTEGER NOT NULL DEFAULT " + Consts.DefaultAIMaxSimultaneousConnections);
            EnsureColumn(connection, "AISettings", "IssueSimilarityEnabled", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumn(connection, "AISettings", "IssueSimilarityThreshold", "REAL NOT NULL DEFAULT " + Consts.DefaultIssueSimilarityThreshold);
            EnsureColumn(connection, "AISettings", "IssueSimilarityMinimumTokenLength", "INTEGER NOT NULL DEFAULT " + Consts.DefaultIssueSimilarityMinimumTokenLength);
            EnsureColumn(connection, "AISettings", "IssueDefaultPageSize", "INTEGER NOT NULL DEFAULT " + Consts.IssueDefaultPageSize);
            EnsureColumn(connection, "AISettings", "IssuePageSizeOptions", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "ReviewPrompt", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "SystemPrompt", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "ReviewDefinitionVersion", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "EngineName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "EngineProvider", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "EngineModel", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "RequestedContextKeysJson", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "ReviewContexts", "SystemPromptVersionId", "INTEGER NULL");
            EnsureColumn(connection, "ReviewContexts", "ReviewPromptVersionId", "INTEGER NULL");
            EnsureColumn(connection, "ReviewContexts", "Branch", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "IssueValidations", "IsAutomatic", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "ReviewUsage", "Branch", "TEXT NOT NULL DEFAULT ''");
        }

        private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
            long count = (long)command.ExecuteScalar();

            if (count == 0)
            {
                command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                command.ExecuteNonQuery();
            }
        }

        private static void RebuildLegacyReviewSchema(SqliteConnection connection)
        {
            if (!IsLegacyReviewSchema(connection))
            {
                return;
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();

            foreach (string table in GetTableNames(connection))
            {
                command.CommandText = $"DROP TABLE IF EXISTS \"{table}\";";
                command.ExecuteNonQuery();
            }

            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        private static bool IsLegacyReviewSchema(SqliteConnection connection)
        {
            return TableExists(connection, "ReviewTypes") && ColumnExists(connection, "ReviewTypes", "RepositoryId");
        }

        private static IReadOnlyList<string> GetTableNames(SqliteConnection connection)
        {
            List<string> tables = [];
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static bool TableExists(SqliteConnection connection, string table)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @Table;";
            command.Parameters.AddWithValue("@Table", table);
            return (long)command.ExecuteScalar() > 0;
        }

        private static bool ColumnExists(SqliteConnection connection, string table, string column)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
            return (long)command.ExecuteScalar() > 0;
        }
    }
}
