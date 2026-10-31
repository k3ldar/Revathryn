using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IQueueRepository"/>.</summary>
    public sealed class QueueRepository : SqliteRepositoryBase, IQueueRepository
    {
        private const string SelectColumns = "Id, RepositoryId, RepoFileId, ReviewTypeId, Priority, CreatedDate";

        /// <summary>Initialises a new repository.</summary>
        public QueueRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a queue item if an equivalent pending item does not already exist.</summary>
        public async Task<bool> EnqueueAsync(QueueItem item, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO Queue (RepositoryId, RepoFileId, ReviewTypeId, Priority, CreatedDate)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @Priority, @CreatedDate);
                """;
            AddParameters(command, item);

            int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected > 0;
        }

        /// <summary>Removes and returns the highest priority pending item, or null when empty.</summary>
        public async Task<QueueItem> DequeueAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM Queue
                WHERE Id = (SELECT Id FROM Queue ORDER BY Priority DESC, CreatedDate ASC LIMIT 1)
                RETURNING Id, RepositoryId, RepoFileId, ReviewTypeId, Priority, CreatedDate;
                """;

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapQueueItem(reader);
            }

            return null;
        }

        /// <summary>Returns pending items ordered by priority descending, then creation ascending.</summary>
        public async Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Queue ORDER BY Priority DESC, CreatedDate ASC;";

            List<QueueItem> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapQueueItem(reader));
            }

            return results;
        }

        /// <summary>Returns pending items for a repository, ordered as in <see cref="GetPendingAsync"/>.</summary>
        public async Task<IReadOnlyList<QueueItem>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Queue WHERE RepositoryId = @RepositoryId ORDER BY Priority DESC, CreatedDate ASC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<QueueItem> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapQueueItem(reader));
            }

            return results;
        }

        /// <summary>Removes the queue item with the given id.</summary>
        public async Task RemoveAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Queue WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Removes all pending items for a file.</summary>
        public async Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Queue WHERE RepoFileId = @RepoFileId;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the number of pending items.</summary>
        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Queue;";

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        private static void AddParameters(SqliteCommand command, QueueItem item)
        {
            command.Parameters.AddWithValue("@RepositoryId", item.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", item.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", item.ReviewTypeId);
            command.Parameters.AddWithValue("@Priority", item.Priority);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(item.CreatedDate));
        }

        private static QueueItem MapQueueItem(SqliteDataReader reader)
        {
            return new QueueItem
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                ReviewTypeId = reader.GetInt64(3),
                Priority = reader.GetInt32(4),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(5))
            };
        }
    }
}
