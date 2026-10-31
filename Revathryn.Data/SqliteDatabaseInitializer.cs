using Microsoft.Data.Sqlite;

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
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = string.Join(
                Environment.NewLine,
                SqliteDatabaseSchema.CreateRepositories,
                SqliteDatabaseSchema.CreateRepoFiles,
                SqliteDatabaseSchema.CreateReviewTypes,
                SqliteDatabaseSchema.CreateReviewLinks,
                SqliteDatabaseSchema.CreateRepoFileAnalysis,
                SqliteDatabaseSchema.CreateQueue,
                SqliteDatabaseSchema.CreateFindings,
                SqliteDatabaseSchema.CreateAIEngines,
                SqliteDatabaseSchema.CreateAISettings,
                SqliteDatabaseSchema.CreateRepoProjects,
                SqliteDatabaseSchema.CreateRepositoryScans);

            command.ExecuteNonQuery();

            foreach (string indexStatement in SqliteDatabaseSchema.CreateIndexes)
            {
                command.CommandText = indexStatement;
                command.ExecuteNonQuery();
            }

            ApplyColumnMigrations(connection);
        }

        private static void ApplyColumnMigrations(SqliteConnection connection)
        {
            EnsureColumn(connection, "ReviewTypes", "ReviewDefinitionVersion", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "RepoFileAnalysis", "ReviewDefinitionVersion", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Repositories", "ScanOptions", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "Repositories", "LastSuccessfulScanDate", "TEXT NULL");
            EnsureColumn(connection, "RepoFiles", "IsRemoved", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "RepoFiles", "ProjectPath", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "RepositoryScans", "RenamedFiles", "INTEGER NOT NULL DEFAULT 0");
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
    }
}
