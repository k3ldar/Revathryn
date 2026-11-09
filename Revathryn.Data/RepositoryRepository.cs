using System.Text.Json;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepositoryRepository"/>.</summary>
    public sealed class RepositoryRepository : SqliteRepositoryBase, IRepositoryRepository
    {
        private const string SelectColumns =
            "Id, Name, Description, LocalPath, RepositoryUri, Enabled, AutomaticReviewEnabled, MaxContextRequests, AutoReviewMinimumSeverity, AutoReviewConfidenceThreshold, AIEngineId, ScanOptions, LastSuccessfulScanDate, CreatedDate, LastModifiedDate";

        /// <summary>Initialises a new repository.</summary>
        public RepositoryRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the repository with the given id, or null when not found.</summary>
        public async Task<Repository> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Repositories WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapRepository(reader);
            }

            return null;
        }

        /// <summary>Returns all repositories.</summary>
        public async Task<IReadOnlyList<Repository>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Repositories ORDER BY Name ASC;";

            List<Repository> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRepository(reader));
            }

            return results;
        }

        /// <summary>Returns all enabled repositories.</summary>
        public async Task<IReadOnlyList<Repository>> GetEnabledAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Repositories WHERE Enabled = 1 ORDER BY Name ASC;";

            List<Repository> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRepository(reader));
            }

            return results;
        }

        /// <summary>Inserts the repository and returns its assigned id.</summary>
        public async Task<long> AddAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Repositories (Name, Description, LocalPath, RepositoryUri, Enabled, AutomaticReviewEnabled, MaxContextRequests, AutoReviewMinimumSeverity, AutoReviewConfidenceThreshold, AIEngineId, ScanOptions, LastSuccessfulScanDate, CreatedDate, LastModifiedDate)
                VALUES (@Name, @Description, @LocalPath, @RepositoryUri, @Enabled, @AutomaticReviewEnabled, @MaxContextRequests, @AutoReviewMinimumSeverity, @AutoReviewConfidenceThreshold, @AIEngineId, @ScanOptions, @LastSuccessfulScanDate, @CreatedDate, @LastModifiedDate);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, repository);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an existing repository.</summary>
        public async Task UpdateAsync(Repository repository, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Repositories
                SET Name = @Name,
                    Description = @Description,
                    LocalPath = @LocalPath,
                    RepositoryUri = @RepositoryUri,
                    Enabled = @Enabled,
                    AutomaticReviewEnabled = @AutomaticReviewEnabled,
                    MaxContextRequests = @MaxContextRequests,
                    AutoReviewMinimumSeverity = @AutoReviewMinimumSeverity,
                    AutoReviewConfidenceThreshold = @AutoReviewConfidenceThreshold,
                    AIEngineId = @AIEngineId,
                    ScanOptions = @ScanOptions,
                    LastSuccessfulScanDate = @LastSuccessfulScanDate,
                    CreatedDate = @CreatedDate,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
                """;
            AddParameters(command, repository);
            command.Parameters.AddWithValue("@Id", repository.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the repository with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Repositories WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, Repository repository)
        {
            command.Parameters.AddWithValue("@Name", repository.Name);
            command.Parameters.AddWithValue("@Description", repository.Description);
            command.Parameters.AddWithValue("@LocalPath", repository.LocalPath);
            command.Parameters.AddWithValue("@RepositoryUri", repository.RepositoryUri);
            command.Parameters.AddWithValue("@Enabled", repository.Enabled);
            command.Parameters.AddWithValue("@AutomaticReviewEnabled", repository.AutomaticReviewEnabled);
            command.Parameters.AddWithValue("@MaxContextRequests", repository.MaxContextRequests);
            command.Parameters.AddWithValue("@AutoReviewMinimumSeverity", (int)repository.AutoReviewMinimumSeverity);
            command.Parameters.AddWithValue("@AutoReviewConfidenceThreshold", repository.AutoReviewConfidenceThreshold);
            command.Parameters.AddWithValue("@AIEngineId", ToNullableLong(repository.AIEngineId));
            command.Parameters.AddWithValue("@ScanOptions", SerializeScanOptions(repository.ScanOptions));
            command.Parameters.AddWithValue("@LastSuccessfulScanDate", ToNullableText(repository.LastSuccessfulScanDate));
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(repository.CreatedDate));
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(repository.LastModifiedDate));
        }

        private static string SerializeScanOptions(RepositoryScanOptions options)
        {
            return options is null ? string.Empty : JsonSerializer.Serialize(options);
        }

        private static RepositoryScanOptions DeserializeScanOptions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RepositoryScanOptions>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static object ToNullableText(DateTime? value)
        {
            return value.HasValue ? SqliteValueConverter.ToText(value.Value) : DBNull.Value;
        }

        private static object ToNullableLong(long? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static Repository MapRepository(SqliteDataReader reader)
        {
            return new Repository
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                LocalPath = reader.GetString(3),
                RepositoryUri = reader.GetString(4),
                Enabled = reader.GetBoolean(5),
                AutomaticReviewEnabled = reader.GetBoolean(6),
                MaxContextRequests = reader.GetInt32(7),
                AutoReviewMinimumSeverity = (FindingSeverity)reader.GetInt32(8),
                AutoReviewConfidenceThreshold = reader.GetInt32(9),
                AIEngineId = reader.IsDBNull(10) ? (long?)null : reader.GetInt64(10),
                ScanOptions = DeserializeScanOptions(reader.GetString(11)),
                LastSuccessfulScanDate = reader.IsDBNull(12) ? (DateTime?)null : SqliteValueConverter.ToDateTime(reader.GetString(12)),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(13)),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(14))
            };
        }
    }
}
