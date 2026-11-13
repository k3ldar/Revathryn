using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IQueueRepository"/>.</summary>
    public sealed class QueueRepository : SqliteRepositoryBase, IQueueRepository
    {
        private const string SelectColumns = "q.Id, q.RepositoryId, q.RepoFileId, q.ReviewTypeId, q.CreatedDate, q.AttemptCount, q.MaxAttempts, q.NextAttemptUtc, q.LastError, q.LeasedUntilUtc, q.Status, q.Branch";

        private const string ReturningColumns = "Id, RepositoryId, RepoFileId, ReviewTypeId, CreatedDate, AttemptCount, MaxAttempts, NextAttemptUtc, LastError, LeasedUntilUtc, Status, Branch";

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
                INSERT OR IGNORE INTO Queue (RepositoryId, RepoFileId, ReviewTypeId, CreatedDate, AttemptCount, MaxAttempts, NextAttemptUtc, LastError, LeasedUntilUtc, Status, Branch)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @CreatedDate, @AttemptCount, @MaxAttempts, @NextAttemptUtc, @LastError, @LeasedUntilUtc, @Status, @Branch);
                """;
            AddParameters(command, item);

            int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected > 0;
        }

        /// <summary>
        /// Reclaims stale leases and claims the highest priority eligible pending item.
        /// Priority is resolved live from the linked review type definition.
        /// The returned item is leased until <paramref name="leasedUntilUtc"/> and has its attempt count incremented.
        /// </summary>
        public async Task<QueueItem> ClaimNextAsync(DateTime leasedUntilUtc, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            string now = SqliteValueConverter.ToText(DateTime.UtcNow);
            string lease = SqliteValueConverter.ToText(leasedUntilUtc);

            using (SqliteCommand reclaim = connection.CreateCommand())
            {
                reclaim.Transaction = transaction;
                reclaim.CommandText = """
                    UPDATE Queue
                    SET Status = @Pending, LeasedUntilUtc = NULL
                    WHERE Status = @InFlight AND LeasedUntilUtc < @Now;
                    """;
                reclaim.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);
                reclaim.Parameters.AddWithValue("@InFlight", (int)QueueItemStatus.InFlight);
                reclaim.Parameters.AddWithValue("@Now", now);
                await reclaim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using (SqliteCommand claim = connection.CreateCommand())
            {
                claim.Transaction = transaction;
                claim.CommandText = $"""
                    UPDATE Queue
                    SET Status = @InFlight,
                        LeasedUntilUtc = @LeasedUntilUtc,
                        AttemptCount = AttemptCount + 1
                    WHERE Id = (
                        SELECT q.Id
                        FROM Queue q
                        INNER JOIN ReviewTypes rt ON rt.Id = q.ReviewTypeId
                        WHERE q.Status = @Pending AND (q.NextAttemptUtc IS NULL OR q.NextAttemptUtc <= @Now)
                        ORDER BY rt.DefaultPriority DESC, q.CreatedDate ASC
                        LIMIT 1
                    )
                    RETURNING {ReturningColumns};
                    """;
                claim.Parameters.AddWithValue("@InFlight", (int)QueueItemStatus.InFlight);
                claim.Parameters.AddWithValue("@LeasedUntilUtc", lease);
                claim.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);
                claim.Parameters.AddWithValue("@Now", now);

                QueueItem claimedItem = null;

                using (SqliteDataReader reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        claimedItem = MapQueueItem(reader);
                    }
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return claimedItem;
            }
        }

        /// <summary>Returns the claimed item to the pending state for a later retry.</summary>
        public async Task ReleaseForRetryAsync(long id, DateTime nextAttemptUtc, string lastError, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Queue
                SET Status = @Pending,
                    NextAttemptUtc = @NextAttemptUtc,
                    LastError = @LastError,
                    LeasedUntilUtc = NULL
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToText(nextAttemptUtc));
            command.Parameters.AddWithValue("@LastError", lastError ?? string.Empty);
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Removes the item because it completed successfully.</summary>
        public async Task MarkCompletedAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Queue WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns pending items ordered by live review type priority descending, then creation ascending.</summary>
        public async Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM Queue q
                INNER JOIN ReviewTypes rt ON rt.Id = q.ReviewTypeId
                WHERE q.Status = @Pending
                ORDER BY rt.DefaultPriority DESC, q.CreatedDate ASC;
                """;
            command.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);

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
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM Queue q
                INNER JOIN ReviewTypes rt ON rt.Id = q.ReviewTypeId
                WHERE q.Status = @Pending AND q.RepositoryId = @RepositoryId
                ORDER BY rt.DefaultPriority DESC, q.CreatedDate ASC;
                """;
            command.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);
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
            command.CommandText = "SELECT COUNT(*) FROM Queue WHERE Status = @Pending;";
            command.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        /// <summary>Returns aggregated queue reliability metrics.</summary>
        public async Task<QueueMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    COALESCE(SUM(CASE WHEN Status = @Pending THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Status = @Pending AND AttemptCount > 0 THEN 1 ELSE 0 END), 0),
                    MIN(CASE WHEN Status = @Pending THEN CreatedDate END)
                FROM Queue;
                """;
            command.Parameters.AddWithValue("@Pending", (int)QueueItemStatus.Pending);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                DateTime? oldestPendingUtc = reader.IsDBNull(2) ? (DateTime?)null : SqliteValueConverter.ToDateTime(reader.GetString(2));

                return new QueueMetrics
                {
                    PendingCount = Convert.ToInt32(reader.GetValue(0)),
                    RetryCount = Convert.ToInt32(reader.GetValue(1)),
                    OldestPendingAge = oldestPendingUtc.HasValue ? DateTime.UtcNow - oldestPendingUtc.Value : (TimeSpan?)null
                };
            }

            return new QueueMetrics();
        }

        private static void AddParameters(SqliteCommand command, QueueItem item)
        {
            command.Parameters.AddWithValue("@RepositoryId", item.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", item.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", item.ReviewTypeId);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(item.CreatedDate));
            command.Parameters.AddWithValue("@AttemptCount", item.AttemptCount);
            command.Parameters.AddWithValue("@MaxAttempts", item.MaxAttempts);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToParameter(item.NextAttemptUtc));
            command.Parameters.AddWithValue("@LastError", item.LastError ?? string.Empty);
            command.Parameters.AddWithValue("@LeasedUntilUtc", SqliteValueConverter.ToParameter(item.LeasedUntilUtc));
            command.Parameters.AddWithValue("@Status", (int)item.Status);
            command.Parameters.AddWithValue("@Branch", item.Branch ?? string.Empty);
        }

        private static QueueItem MapQueueItem(SqliteDataReader reader)
        {
            return new QueueItem
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                ReviewTypeId = reader.GetInt64(3),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(4)),
                AttemptCount = reader.GetInt32(5),
                MaxAttempts = reader.GetInt32(6),
                NextAttemptUtc = reader.IsDBNull(7) ? (DateTime?)null : SqliteValueConverter.ToDateTime(reader.GetString(7)),
                LastError = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                LeasedUntilUtc = reader.IsDBNull(9) ? (DateTime?)null : SqliteValueConverter.ToDateTime(reader.GetString(9)),
                Status = (QueueItemStatus)reader.GetInt32(10),
                Branch = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            };
        }
    }
}
