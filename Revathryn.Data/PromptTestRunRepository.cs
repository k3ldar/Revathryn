using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IPromptTestRunRepository"/>.</summary>
    public sealed class PromptTestRunRepository : SqliteRepositoryBase, IPromptTestRunRepository
    {
        private const int SqliteConstraintUniqueExtendedCode = 2067;

        private const string SelectColumns =
            "Id, PromptVersionId, ReviewTypeId, OriginalIssueId, DetectionModel, ReviewContextId, ContextSnapshotJson, PromptText, OriginalOutputJson, TestOutputJson, ComparisonJson, " +
            "Status, AttemptCount, CreatedUtc, StartedUtc, CompletedUtc, NextAttemptUtc, LeasedUntilUtc, " +
            "Provider, Model, EngineConfigSnapshotJson, ErrorMessage, FailureClassification, PromptTokens, CompletionTokens, TotalTokens";

        /// <summary>Initialises a new repository.</summary>
        public PromptTestRunRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a pending test run, or returns 0 when the same version and issue already have an active one.</summary>
        public async Task<long> TryAddAsync(PromptTestRun testRun, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(testRun);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PromptTestRuns (
                    PromptVersionId, ReviewTypeId, OriginalIssueId, PromptText, Status, AttemptCount, CreatedUtc)
                VALUES (
                    @PromptVersionId, @ReviewTypeId, @OriginalIssueId, @PromptText, @Status, 0, @CreatedUtc);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@PromptVersionId", testRun.PromptVersionId);
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(testRun.ReviewTypeId));
            command.Parameters.AddWithValue("@OriginalIssueId", SqliteValueConverter.ToParameter(testRun.OriginalIssueId));
            command.Parameters.AddWithValue("@PromptText", testRun.PromptText ?? string.Empty);
            command.Parameters.AddWithValue("@Status", (int)PromptTestRunStatus.Pending);
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(testRun.CreatedUtc));

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

        /// <summary>Returns a test run by id, or null when not found.</summary>
        public async Task<PromptTestRun> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptTestRuns WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns all test runs for a prompt version, newest first.</summary>
        public async Task<IReadOnlyList<PromptTestRun>> GetByPromptVersionAsync(long promptVersionId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptTestRuns WHERE PromptVersionId = @PromptVersionId ORDER BY CreatedUtc DESC, Id DESC;";
            command.Parameters.AddWithValue("@PromptVersionId", promptVersionId);

            List<PromptTestRun> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Returns the pending or running test run for a prompt version and original issue, or null.</summary>
        public async Task<PromptTestRun> GetActiveByVersionAndIssueAsync(long promptVersionId, long? originalIssueId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM PromptTestRuns
                WHERE PromptVersionId = @PromptVersionId
                  AND OriginalIssueId IS @OriginalIssueId
                  AND Status IN (@Pending, @Running)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@PromptVersionId", promptVersionId);
            command.Parameters.AddWithValue("@OriginalIssueId", SqliteValueConverter.ToParameter(originalIssueId));
            command.Parameters.AddWithValue("@Pending", (int)PromptTestRunStatus.Pending);
            command.Parameters.AddWithValue("@Running", (int)PromptTestRunStatus.Running);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Claims the next due test run under a lease, reclaiming any whose lease expired.</summary>
        public async Task<PromptTestRun> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            string now = SqliteValueConverter.ToText(utcNow);
            string lease = SqliteValueConverter.ToText(utcNow.Add(leaseDuration));

            using (SqliteCommand reclaim = connection.CreateCommand())
            {
                reclaim.Transaction = transaction;
                reclaim.CommandText = """
                    UPDATE PromptTestRuns
                    SET Status = @Pending, LeasedUntilUtc = NULL
                    WHERE Status = @Running AND LeasedUntilUtc < @Now;
                    """;
                reclaim.Parameters.AddWithValue("@Pending", (int)PromptTestRunStatus.Pending);
                reclaim.Parameters.AddWithValue("@Running", (int)PromptTestRunStatus.Running);
                reclaim.Parameters.AddWithValue("@Now", now);
                _ = await reclaim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using SqliteCommand claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = $"""
                UPDATE PromptTestRuns
                SET Status = @Running,
                    LeasedUntilUtc = @Lease,
                    AttemptCount = AttemptCount + 1,
                    StartedUtc = COALESCE(StartedUtc, @Now)
                WHERE Id = (
                    SELECT Id FROM PromptTestRuns
                    WHERE Status = @Pending AND (NextAttemptUtc IS NULL OR NextAttemptUtc <= @Now)
                    ORDER BY CreatedUtc ASC, Id ASC
                    LIMIT 1
                )
                RETURNING {SelectColumns};
                """;
            claim.Parameters.AddWithValue("@Running", (int)PromptTestRunStatus.Running);
            claim.Parameters.AddWithValue("@Pending", (int)PromptTestRunStatus.Pending);
            claim.Parameters.AddWithValue("@Lease", lease);
            claim.Parameters.AddWithValue("@Now", now);

            PromptTestRun claimed = null;

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

        /// <summary>Records the context, prompt and engine configuration about to be tested.</summary>
        public async Task SaveExecutionSnapshotAsync(PromptTestRun testRun, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(testRun);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptTestRuns
                SET ReviewTypeId = @ReviewTypeId,
                    DetectionModel = @DetectionModel,
                    ReviewContextId = @ReviewContextId,
                    ContextSnapshotJson = @ContextSnapshotJson,
                    PromptText = @PromptText,
                    OriginalOutputJson = @OriginalOutputJson,
                    Provider = @Provider,
                    Model = @Model,
                    EngineConfigSnapshotJson = @EngineConfigSnapshotJson
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", testRun.Id);
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(testRun.ReviewTypeId));
            command.Parameters.AddWithValue("@DetectionModel", testRun.DetectionModel ?? string.Empty);
            command.Parameters.AddWithValue("@ReviewContextId", SqliteValueConverter.ToParameter(testRun.ReviewContextId));
            command.Parameters.AddWithValue("@ContextSnapshotJson", testRun.ContextSnapshotJson ?? string.Empty);
            command.Parameters.AddWithValue("@PromptText", testRun.PromptText ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalOutputJson", testRun.OriginalOutputJson ?? string.Empty);
            command.Parameters.AddWithValue("@Provider", (int)testRun.Provider);
            command.Parameters.AddWithValue("@Model", testRun.Model ?? string.Empty);
            command.Parameters.AddWithValue("@EngineConfigSnapshotJson", testRun.EngineConfigSnapshotJson ?? string.Empty);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Persists the revised output and comparison and marks the test run completed.</summary>
        public async Task CompleteAsync(PromptTestRun testRun, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(testRun);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptTestRuns
                SET Status = @Completed,
                    CompletedUtc = @CompletedUtc,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    TestOutputJson = @TestOutputJson,
                    ComparisonJson = @ComparisonJson,
                    ErrorMessage = '',
                    FailureClassification = NULL,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", testRun.Id);
            command.Parameters.AddWithValue("@Completed", (int)PromptTestRunStatus.Completed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(testRun.CompletedUtc ?? DateTime.UtcNow));
            command.Parameters.AddWithValue("@TestOutputJson", testRun.TestOutputJson ?? string.Empty);
            command.Parameters.AddWithValue("@ComparisonJson", testRun.ComparisonJson ?? string.Empty);
            command.Parameters.AddWithValue("@PromptTokens", testRun.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", testRun.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", testRun.TotalTokens);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Marks the test run failed.</summary>
        public async Task FailAsync(
            long id,
            string errorMessage,
            FailureCategory category,
            string rawResponse,
            TokenUsage usage,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptTestRuns
                SET Status = @Failed,
                    CompletedUtc = @CompletedUtc,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    ErrorMessage = @ErrorMessage,
                    FailureClassification = @FailureClassification,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Failed", (int)PromptTestRunStatus.Failed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(utcNow));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            command.Parameters.AddWithValue("@FailureClassification", (int)category);
            AddUsageParameters(command, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the test run to the pending state for a later retry.</summary>
        public async Task ReleaseForRetryAsync(
            long id,
            DateTime nextAttemptUtc,
            string errorMessage,
            string rawResponse,
            TokenUsage usage,
            CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptTestRuns
                SET Status = @Pending,
                    NextAttemptUtc = @NextAttemptUtc,
                    LeasedUntilUtc = NULL,
                    ErrorMessage = @ErrorMessage,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Pending", (int)PromptTestRunStatus.Pending);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToText(nextAttemptUtc));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            AddUsageParameters(command, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddUsageParameters(SqliteCommand command, TokenUsage usage)
        {
            TokenUsage safeUsage = usage ?? new TokenUsage();
            command.Parameters.AddWithValue("@PromptTokens", safeUsage.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", safeUsage.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", safeUsage.TotalTokens);
        }

        private static async Task<PromptTestRun> ReadSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
        {
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        private static DateTime? ReadNullableDate(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : SqliteValueConverter.ToDateTime(reader.GetString(ordinal));
        }

        private static PromptTestRun Map(SqliteDataReader reader)
        {
            return new PromptTestRun
            {
                Id = reader.GetInt64(0),
                PromptVersionId = reader.GetInt64(1),
                ReviewTypeId = reader.GetNullableInt64(2),
                OriginalIssueId = reader.GetNullableInt64(3),
                DetectionModel = reader.GetString(4),
                ReviewContextId = reader.GetNullableInt64(5),
                ContextSnapshotJson = reader.GetString(6),
                PromptText = reader.GetString(7),
                OriginalOutputJson = reader.GetString(8),
                TestOutputJson = reader.GetString(9),
                ComparisonJson = reader.GetString(10),
                Status = (PromptTestRunStatus)reader.GetInt32(11),
                AttemptCount = reader.GetInt32(12),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(13)),
                StartedUtc = ReadNullableDate(reader, 14),
                CompletedUtc = ReadNullableDate(reader, 15),
                NextAttemptUtc = ReadNullableDate(reader, 16),
                LeasedUntilUtc = ReadNullableDate(reader, 17),
                Provider = (AIProvider)reader.GetInt32(18),
                Model = reader.GetString(19),
                EngineConfigSnapshotJson = reader.GetString(20),
                ErrorMessage = reader.GetString(21),
                FailureClassification = reader.IsDBNull(22) ? null : (FailureCategory)reader.GetInt32(22),
                PromptTokens = reader.GetInt32(23),
                CompletionTokens = reader.GetInt32(24),
                TotalTokens = reader.GetInt32(25)
            };
        }
    }
}
