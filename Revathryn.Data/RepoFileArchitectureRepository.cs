using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepoFileArchitectureRepository"/>.</summary>
    public sealed class RepoFileArchitectureRepository : SqliteRepositoryBase, IRepoFileArchitectureRepository
    {
        private const string SelectColumns = "Id, RepositoryId, RepoFileId, Sha256, Summary, EngineName, EngineModel, AnalysisVersion, CreatedUtc";

        /// <summary>Initialises a new repository.</summary>
        public RepoFileArchitectureRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the cached summary for a file, or null when not found.</summary>
        public async Task<RepoFileArchitecture> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFileArchitectures WHERE RepoFileId = @RepoFileId LIMIT 1;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        /// <summary>Inserts or updates the cached summary and returns its id.</summary>
        public async Task<long> UpsertAsync(RepoFileArchitecture architecture, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await UpsertAsync(connection, null, architecture, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts or updates the cached summary within the supplied transaction and returns its id.</summary>
        internal static async Task<long> UpsertAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            RepoFileArchitecture architecture,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO RepoFileArchitectures (RepositoryId, RepoFileId, Sha256, Summary, EngineName, EngineModel, AnalysisVersion, CreatedUtc)
                VALUES (@RepositoryId, @RepoFileId, @Sha256, @Summary, @EngineName, @EngineModel, @AnalysisVersion, @CreatedUtc)
                ON CONFLICT (RepoFileId) DO UPDATE SET
                    RepositoryId = excluded.RepositoryId,
                    Sha256 = excluded.Sha256,
                    Summary = excluded.Summary,
                    EngineName = excluded.EngineName,
                    EngineModel = excluded.EngineModel,
                    AnalysisVersion = excluded.AnalysisVersion,
                    CreatedUtc = excluded.CreatedUtc;
                SELECT Id FROM RepoFileArchitectures WHERE RepoFileId = @RepoFileId LIMIT 1;
                """;
            AddParameters(command, architecture);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Deletes the cached summary for a file.</summary>
        public async Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFileArchitectures WHERE RepoFileId = @RepoFileId;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes all cached summaries belonging to a repository.</summary>
        public async Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFileArchitectures WHERE RepositoryId = @RepositoryId;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, RepoFileArchitecture architecture)
        {
            command.Parameters.AddWithValue("@RepositoryId", architecture.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", architecture.RepoFileId);
            command.Parameters.AddWithValue("@Sha256", architecture.Sha256);
            command.Parameters.AddWithValue("@Summary", architecture.Summary);
            command.Parameters.AddWithValue("@EngineName", architecture.EngineName);
            command.Parameters.AddWithValue("@EngineModel", architecture.EngineModel);
            command.Parameters.AddWithValue("@AnalysisVersion", architecture.AnalysisVersion);
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(architecture.CreatedUtc));
        }

        private static RepoFileArchitecture Map(SqliteDataReader reader)
        {
            return new RepoFileArchitecture
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                Sha256 = reader.GetString(3),
                Summary = reader.GetString(4),
                EngineName = reader.GetString(5),
                EngineModel = reader.GetString(6),
                AnalysisVersion = reader.GetString(7),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(8))
            };
        }
    }
}
