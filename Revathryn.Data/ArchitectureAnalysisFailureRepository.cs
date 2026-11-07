using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IArchitectureAnalysisFailureRepository"/>.</summary>
    public sealed class ArchitectureAnalysisFailureRepository : SqliteRepositoryBase, IArchitectureAnalysisFailureRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RepoFileId, FilePath, Language, Sha256, SystemPrompt, FileContent, Error, RawResponse, EngineName, EngineModel, CreatedUtc";

        /// <summary>Initialises a new repository.</summary>
        public ArchitectureAnalysisFailureRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts the failure and returns its assigned id.</summary>
        public async Task<long> AddAsync(ArchitectureAnalysisFailure failure, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ArchitectureAnalysisFailures (RepositoryId, RepoFileId, FilePath, Language, Sha256, SystemPrompt, FileContent, Error, RawResponse, EngineName, EngineModel, CreatedUtc)
                VALUES (@RepositoryId, @RepoFileId, @FilePath, @Language, @Sha256, @SystemPrompt, @FileContent, @Error, @RawResponse, @EngineName, @EngineModel, @CreatedUtc);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, failure);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Returns all failures, most recent first.</summary>
        public async Task<IReadOnlyList<ArchitectureAnalysisFailure>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ArchitectureAnalysisFailures ORDER BY CreatedUtc DESC;";

            List<ArchitectureAnalysisFailure> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Returns the failure with the given id, or null when not found.</summary>
        public async Task<ArchitectureAnalysisFailure> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ArchitectureAnalysisFailures WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        private static void AddParameters(SqliteCommand command, ArchitectureAnalysisFailure failure)
        {
            command.Parameters.AddWithValue("@RepositoryId", failure.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", SqliteValueConverter.ToParameter(failure.RepoFileId));
            command.Parameters.AddWithValue("@FilePath", failure.FilePath);
            command.Parameters.AddWithValue("@Language", failure.Language);
            command.Parameters.AddWithValue("@Sha256", failure.Sha256);
            command.Parameters.AddWithValue("@SystemPrompt", failure.SystemPrompt);
            command.Parameters.AddWithValue("@FileContent", failure.FileContent);
            command.Parameters.AddWithValue("@Error", failure.Error);
            command.Parameters.AddWithValue("@RawResponse", failure.RawResponse);
            command.Parameters.AddWithValue("@EngineName", failure.EngineName);
            command.Parameters.AddWithValue("@EngineModel", failure.EngineModel);
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(failure.CreatedUtc));
        }

        private static ArchitectureAnalysisFailure Map(SqliteDataReader reader)
        {
            return new ArchitectureAnalysisFailure
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetNullableInt64(2),
                FilePath = reader.GetString(3),
                Language = reader.GetString(4),
                Sha256 = reader.GetString(5),
                SystemPrompt = reader.GetString(6),
                FileContent = reader.GetString(7),
                Error = reader.GetString(8),
                RawResponse = reader.GetString(9),
                EngineName = reader.GetString(10),
                EngineModel = reader.GetString(11),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(12))
            };
        }
    }
}
