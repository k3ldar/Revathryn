using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IDuplicateDetectionQueue"/>.</summary>
    public sealed class DuplicateDetectionRepository : SqliteRepositoryBase, IDuplicateDetectionQueue
    {
        private const int SqliteConstraintUniqueExtendedCode = 2067;

        private const string SelectColumns =
            "Id, RepositoryId, FindingId, Status, AttemptCount, CreatedUtc, NextAttemptUtc, LeasedUntilUtc, LastError";

        /// <summary>Initialises a new repository.</summary>
        public DuplicateDetectionRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Enqueues a finding for duplicate evaluation, or returns 0 when an active item already exists.</summary>
        public async Task<long> EnqueueAsync(long repositoryId, long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO DuplicateDetectionQueue (RepositoryId, FindingId, Status, AttemptCount, CreatedUtc)
                VALUES (@RepositoryId, @FindingId, @Status, 0, @CreatedUtc);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@FindingId", findingId);
            command.Parameters.AddWithValue("@Status", (int)DuplicateDetectionStatus.Pending);
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(DateTime.UtcNow));

            try
            {
                object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return (long)result;
            }
            catch (SqliteException exception) when (exception.SqliteExtendedErrorCode == SqliteConstraintUniqueExtendedCode)
            {
                return 0;
            }
        }

        /// <summary>Claims the next due item under a lease, reclaiming any whose lease expired.</summary>
        public async Task<DuplicateDetectionQueueItem> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            string now = SqliteValueConverter.ToText(utcNow);
            string lease = SqliteValueConverter.ToText(utcNow.Add(leaseDuration));

            using (SqliteCommand reclaim = connection.CreateCommand())
            {
                reclaim.Transaction = transaction;
                reclaim.CommandText = """
                    UPDATE DuplicateDetectionQueue
                    SET Status = @Pending, LeasedUntilUtc = NULL
                    WHERE Status = @Running AND LeasedUntilUtc < @Now;
                    """;
                reclaim.Parameters.AddWithValue("@Pending", (int)DuplicateDetectionStatus.Pending);
                reclaim.Parameters.AddWithValue("@Running", (int)DuplicateDetectionStatus.Running);
                reclaim.Parameters.AddWithValue("@Now", now);
                _ = await reclaim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using SqliteCommand claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = $"""
                UPDATE DuplicateDetectionQueue
                SET Status = @Running,
                    LeasedUntilUtc = @Lease,
                    AttemptCount = AttemptCount + 1
                WHERE Id = (
                    SELECT Id FROM DuplicateDetectionQueue
                    WHERE Status = @Pending AND (NextAttemptUtc IS NULL OR NextAttemptUtc <= @Now)
                    ORDER BY CreatedUtc ASC, Id ASC
                    LIMIT 1
                )
                RETURNING {SelectColumns};
                """;
            claim.Parameters.AddWithValue("@Running", (int)DuplicateDetectionStatus.Running);
            claim.Parameters.AddWithValue("@Pending", (int)DuplicateDetectionStatus.Pending);
            claim.Parameters.AddWithValue("@Lease", lease);
            claim.Parameters.AddWithValue("@Now", now);

            DuplicateDetectionQueueItem claimed = null;

            using (SqliteDataReader reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    claimed = Map(reader);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return claimed;
        }

        /// <summary>Marks the item completed when the caller still owns the current attempt.</summary>
        public async Task<bool> CompleteAsync(long id, int attemptCount, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE DuplicateDetectionQueue
                SET Status = @Completed,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    LastError = ''
                WHERE Id = @Id AND Status = @Running AND AttemptCount = @AttemptCount;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Running", (int)DuplicateDetectionStatus.Running);
            command.Parameters.AddWithValue("@AttemptCount", attemptCount);
            command.Parameters.AddWithValue("@Completed", (int)DuplicateDetectionStatus.Completed);

            int rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 1;
        }

        /// <summary>Marks the item permanently failed when the caller still owns the current attempt.</summary>
        public async Task<bool> FailAsync(long id, int attemptCount, string errorMessage, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE DuplicateDetectionQueue
                SET Status = @Failed,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    LastError = @LastError
                WHERE Id = @Id AND Status = @Running AND AttemptCount = @AttemptCount;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Running", (int)DuplicateDetectionStatus.Running);
            command.Parameters.AddWithValue("@AttemptCount", attemptCount);
            command.Parameters.AddWithValue("@Failed", (int)DuplicateDetectionStatus.Failed);
            command.Parameters.AddWithValue("@LastError", errorMessage ?? string.Empty);

            int rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 1;
        }

        /// <summary>Returns the item to the pending state when the caller still owns the current attempt.</summary>
        public async Task<bool> ReleaseForRetryAsync(long id, int attemptCount, DateTime nextAttemptUtc, string errorMessage, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE DuplicateDetectionQueue
                SET Status = @Pending,
                    NextAttemptUtc = @NextAttemptUtc,
                    LeasedUntilUtc = NULL,
                    LastError = @LastError
                WHERE Id = @Id AND Status = @Running AND AttemptCount = @AttemptCount;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Running", (int)DuplicateDetectionStatus.Running);
            command.Parameters.AddWithValue("@AttemptCount", attemptCount);
            command.Parameters.AddWithValue("@Pending", (int)DuplicateDetectionStatus.Pending);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToText(nextAttemptUtc));
            command.Parameters.AddWithValue("@LastError", errorMessage ?? string.Empty);

            int rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 1;
        }

        private static DateTime? ReadNullableDate(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : SqliteValueConverter.ToDateTime(reader.GetString(ordinal));
        }

        private static DuplicateDetectionQueueItem Map(SqliteDataReader reader)
        {
            return new DuplicateDetectionQueueItem
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                FindingId = reader.GetInt64(2),
                Status = (DuplicateDetectionStatus)reader.GetInt32(3),
                AttemptCount = reader.GetInt32(4),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(5)),
                NextAttemptUtc = ReadNullableDate(reader, 6),
                LeasedUntilUtc = ReadNullableDate(reader, 7),
                LastError = reader.GetString(8)
            };
        }
    }
}
