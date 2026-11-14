using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepositoryScanRepository"/>.</summary>
    public sealed class RepositoryScanRepository : SqliteRepositoryBase, IRepositoryScanRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, StartedUtc, CompletedUtc, Succeeded, FilesDiscovered, NewFiles, ChangedFiles, UnchangedFiles, RemovedFiles, RenamedFiles, Branch";

        /// <summary>Initialises a new repository.</summary>
        public RepositoryScanRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a scan record and returns its assigned id.</summary>
        public async Task<long> AddAsync(RepositoryScanRecord record, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO RepositoryScans (RepositoryId, StartedUtc, CompletedUtc, Succeeded, FilesDiscovered, NewFiles, ChangedFiles, UnchangedFiles, RemovedFiles, RenamedFiles, Branch)
                VALUES (@RepositoryId, @StartedUtc, @CompletedUtc, @Succeeded, @FilesDiscovered, @NewFiles, @ChangedFiles, @UnchangedFiles, @RemovedFiles, @RenamedFiles, @Branch);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, record);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Returns scan records for a repository, most recent first.</summary>
        public async Task<IReadOnlyList<RepositoryScanRecord>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepositoryScans WHERE RepositoryId = @RepositoryId ORDER BY StartedUtc DESC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<RepositoryScanRecord> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRecord(reader));
            }

            return results;
        }

        /// <summary>Returns all scan records across repositories, most recent first.</summary>
        public async Task<IReadOnlyList<RepositoryScanRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepositoryScans ORDER BY StartedUtc DESC;";

            List<RepositoryScanRecord> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRecord(reader));
            }

            return results;
        }

        private static void AddParameters(SqliteCommand command, RepositoryScanRecord record)
        {
            command.Parameters.AddWithValue("@RepositoryId", record.RepositoryId);
            command.Parameters.AddWithValue("@StartedUtc", SqliteValueConverter.ToText(record.StartedUtc));
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(record.CompletedUtc));
            command.Parameters.AddWithValue("@Succeeded", record.Succeeded);
            command.Parameters.AddWithValue("@FilesDiscovered", record.FilesDiscovered);
            command.Parameters.AddWithValue("@NewFiles", record.NewFiles);
            command.Parameters.AddWithValue("@ChangedFiles", record.ChangedFiles);
            command.Parameters.AddWithValue("@UnchangedFiles", record.UnchangedFiles);
            command.Parameters.AddWithValue("@RemovedFiles", record.RemovedFiles);
            command.Parameters.AddWithValue("@RenamedFiles", record.RenamedFiles);
            command.Parameters.AddWithValue("@Branch", record.Branch ?? string.Empty);
        }

        private static RepositoryScanRecord MapRecord(SqliteDataReader reader)
        {
            return new RepositoryScanRecord
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                StartedUtc = SqliteValueConverter.ToDateTime(reader.GetString(2)),
                CompletedUtc = SqliteValueConverter.ToDateTime(reader.GetString(3)),
                Succeeded = reader.GetBoolean(4),
                FilesDiscovered = reader.GetInt32(5),
                NewFiles = reader.GetInt32(6),
                ChangedFiles = reader.GetInt32(7),
                UnchangedFiles = reader.GetInt32(8),
                RemovedFiles = reader.GetInt32(9),
                RenamedFiles = reader.GetInt32(10),
                Branch = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            };
        }
    }
}
