using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepoFileAnalysisRepository"/>.</summary>
    public sealed class RepoFileAnalysisRepository : SqliteRepositoryBase, IRepoFileAnalysisRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RepoFileId, ReviewTypeId, Sha256, LastAnalysedDate, AnalysisVersion, ReviewDefinitionVersion";

        /// <summary>Initialises a new repository.</summary>
        public RepoFileAnalysisRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the analysis record for the file and review type, or null when not found.</summary>
        public async Task<RepoFileAnalysis> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFileAnalysis WHERE RepoFileId = @RepoFileId AND ReviewTypeId = @ReviewTypeId LIMIT 1;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapAnalysis(reader);
            }

            return null;
        }

        /// <summary>Inserts or updates the analysis record and returns its id.</summary>
        public async Task<long> UpsertAsync(RepoFileAnalysis analysis, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO RepoFileAnalysis (RepositoryId, RepoFileId, ReviewTypeId, Sha256, LastAnalysedDate, AnalysisVersion, ReviewDefinitionVersion)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @Sha256, @LastAnalysedDate, @AnalysisVersion, @ReviewDefinitionVersion)
                ON CONFLICT (RepoFileId, ReviewTypeId) DO UPDATE SET
                    RepositoryId = excluded.RepositoryId,
                    Sha256 = excluded.Sha256,
                    LastAnalysedDate = excluded.LastAnalysedDate,
                    AnalysisVersion = excluded.AnalysisVersion,
                    ReviewDefinitionVersion = excluded.ReviewDefinitionVersion;
                SELECT Id FROM RepoFileAnalysis WHERE RepoFileId = @RepoFileId AND ReviewTypeId = @ReviewTypeId;
                """;
            AddParameters(command, analysis);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Deletes the analysis record with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFileAnalysis WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes all analysis records for a file.</summary>
        public async Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFileAnalysis WHERE RepoFileId = @RepoFileId;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, RepoFileAnalysis analysis)
        {
            command.Parameters.AddWithValue("@RepositoryId", analysis.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", analysis.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", analysis.ReviewTypeId);
            command.Parameters.AddWithValue("@Sha256", analysis.Sha256);
            command.Parameters.AddWithValue("@LastAnalysedDate", SqliteValueConverter.ToText(analysis.LastAnalysedDate));
            command.Parameters.AddWithValue("@AnalysisVersion", analysis.AnalysisVersion);
            command.Parameters.AddWithValue("@ReviewDefinitionVersion", analysis.ReviewDefinitionVersion);
        }

        private static RepoFileAnalysis MapAnalysis(SqliteDataReader reader)
        {
            return new RepoFileAnalysis
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                ReviewTypeId = reader.GetInt64(3),
                Sha256 = reader.GetString(4),
                LastAnalysedDate = SqliteValueConverter.ToDateTime(reader.GetString(5)),
                AnalysisVersion = reader.GetString(6),
                ReviewDefinitionVersion = reader.GetString(7)
            };
        }
    }
}
