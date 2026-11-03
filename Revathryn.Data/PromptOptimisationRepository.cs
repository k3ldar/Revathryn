using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IPromptOptimisationRepository"/>.</summary>
    public sealed class PromptOptimisationRepository : SqliteRepositoryBase, IPromptOptimisationRepository
    {
        private const int SqliteConstraintUniqueExtendedCode = 2067;

        private const string SelectColumns =
            "Id, IssueValidationId, FindingId, RepositoryId, ReviewTypeId, Status, RetryOfOptimisationId, AttemptCount, CreatedUtc, StartedUtc, CompletedUtc, NextAttemptUtc, LeasedUntilUtc, " +
            "Provider, Model, EngineConfigSnapshotJson, OriginalSystemPrompt, OriginalReviewPrompt, OriginalSystemPromptVersionId, OriginalReviewPromptVersionId, " +
            "AnalysisJson, SystemPromptRecommendationJson, ReviewPromptRecommendationJson, RawResponse, ErrorMessage, FailureClassification, PromptTokens, CompletionTokens, TotalTokens";

        /// <summary>Initialises a new repository.</summary>
        public PromptOptimisationRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a pending optimisation, or returns 0 when the validation already has an active one.</summary>
        public async Task<long> TryAddAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PromptOptimisations (
                    IssueValidationId, FindingId, RepositoryId, ReviewTypeId, Status, RetryOfOptimisationId, AttemptCount, CreatedUtc,
                    OriginalSystemPrompt, OriginalReviewPrompt, OriginalSystemPromptVersionId, OriginalReviewPromptVersionId)
                VALUES (
                    @IssueValidationId, @FindingId, @RepositoryId, @ReviewTypeId, @Status, @RetryOfOptimisationId, 0, @CreatedUtc,
                    @OriginalSystemPrompt, @OriginalReviewPrompt, @OriginalSystemPromptVersionId, @OriginalReviewPromptVersionId);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@IssueValidationId", SqliteValueConverter.ToParameter(optimisation.IssueValidationId));
            command.Parameters.AddWithValue("@FindingId", optimisation.FindingId);
            command.Parameters.AddWithValue("@RepositoryId", optimisation.RepositoryId);
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(optimisation.ReviewTypeId));
            command.Parameters.AddWithValue("@Status", (int)PromptOptimisationStatus.Pending);
            command.Parameters.AddWithValue("@RetryOfOptimisationId", SqliteValueConverter.ToParameter(optimisation.RetryOfOptimisationId));
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(optimisation.CreatedUtc));
            command.Parameters.AddWithValue("@OriginalSystemPrompt", optimisation.OriginalSystemPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalReviewPrompt", optimisation.OriginalReviewPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalSystemPromptVersionId", SqliteValueConverter.ToParameter(optimisation.OriginalSystemPromptVersionId));
            command.Parameters.AddWithValue("@OriginalReviewPromptVersionId", SqliteValueConverter.ToParameter(optimisation.OriginalReviewPromptVersionId));

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

        /// <summary>Returns an optimisation by id, or null when not found.</summary>
        public async Task<PromptOptimisation> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptOptimisations WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns all optimisations for a finding, newest first.</summary>
        public async Task<IReadOnlyList<PromptOptimisation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptOptimisations WHERE FindingId = @FindingId ORDER BY CreatedUtc DESC, Id DESC;";
            command.Parameters.AddWithValue("@FindingId", findingId);

            List<PromptOptimisation> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Returns the pending or running optimisation for a validation, or null.</summary>
        public async Task<PromptOptimisation> GetActiveByValidationAsync(long issueValidationId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM PromptOptimisations
                WHERE IssueValidationId = @IssueValidationId AND Status IN (@Pending, @Running)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@IssueValidationId", issueValidationId);
            command.Parameters.AddWithValue("@Pending", (int)PromptOptimisationStatus.Pending);
            command.Parameters.AddWithValue("@Running", (int)PromptOptimisationStatus.Running);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the most recent completed optimisation for a finding, or null.</summary>
        public async Task<PromptOptimisation> GetLatestCompletedAsync(long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM PromptOptimisations
                WHERE FindingId = @FindingId AND Status = @Completed
                ORDER BY CreatedUtc DESC, Id DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@FindingId", findingId);
            command.Parameters.AddWithValue("@Completed", (int)PromptOptimisationStatus.Completed);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Claims the next due optimisation under a lease, reclaiming any whose lease expired.</summary>
        public async Task<PromptOptimisation> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            string now = SqliteValueConverter.ToText(utcNow);
            string lease = SqliteValueConverter.ToText(utcNow.Add(leaseDuration));

            using (SqliteCommand reclaim = connection.CreateCommand())
            {
                reclaim.Transaction = transaction;
                reclaim.CommandText = """
                    UPDATE PromptOptimisations
                    SET Status = @Pending, LeasedUntilUtc = NULL
                    WHERE Status = @Running AND LeasedUntilUtc < @Now;
                    """;
                reclaim.Parameters.AddWithValue("@Pending", (int)PromptOptimisationStatus.Pending);
                reclaim.Parameters.AddWithValue("@Running", (int)PromptOptimisationStatus.Running);
                reclaim.Parameters.AddWithValue("@Now", now);
                _ = await reclaim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using SqliteCommand claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = $"""
                UPDATE PromptOptimisations
                SET Status = @Running,
                    LeasedUntilUtc = @Lease,
                    AttemptCount = AttemptCount + 1,
                    StartedUtc = COALESCE(StartedUtc, @Now)
                WHERE Id = (
                    SELECT Id FROM PromptOptimisations
                    WHERE Status = @Pending AND (NextAttemptUtc IS NULL OR NextAttemptUtc <= @Now)
                    ORDER BY CreatedUtc ASC, Id ASC
                    LIMIT 1
                )
                RETURNING {SelectColumns};
                """;
            claim.Parameters.AddWithValue("@Running", (int)PromptOptimisationStatus.Running);
            claim.Parameters.AddWithValue("@Pending", (int)PromptOptimisationStatus.Pending);
            claim.Parameters.AddWithValue("@Lease", lease);
            claim.Parameters.AddWithValue("@Now", now);

            PromptOptimisation claimed = null;

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

        /// <summary>Records the engine configuration about to be used.</summary>
        public async Task SaveExecutionSnapshotAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptOptimisations
                SET Provider = @Provider,
                    Model = @Model,
                    EngineConfigSnapshotJson = @EngineConfigSnapshotJson
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", optimisation.Id);
            command.Parameters.AddWithValue("@Provider", (int)optimisation.Provider);
            command.Parameters.AddWithValue("@Model", optimisation.Model ?? string.Empty);
            command.Parameters.AddWithValue("@EngineConfigSnapshotJson", optimisation.EngineConfigSnapshotJson ?? string.Empty);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Persists the parsed disagreement analysis from stage 1.</summary>
        public async Task RecordAnalysisAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptOptimisations
                SET AnalysisJson = @AnalysisJson,
                    RawResponse = @RawResponse,
                    ErrorMessage = '',
                    FailureClassification = NULL,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", optimisation.Id);
            command.Parameters.AddWithValue("@AnalysisJson", optimisation.AnalysisJson ?? string.Empty);
            command.Parameters.AddWithValue("@RawResponse", optimisation.RawResponse ?? string.Empty);
            AddUsageParameters(command, optimisation.PromptTokens, optimisation.CompletionTokens, optimisation.TotalTokens);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Persists the generated recommendations from stage 2 and marks the optimisation completed.</summary>
        public async Task RecordProposalsAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptOptimisations
                SET Status = @Completed,
                    CompletedUtc = @CompletedUtc,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    SystemPromptRecommendationJson = @SystemPromptRecommendationJson,
                    ReviewPromptRecommendationJson = @ReviewPromptRecommendationJson,
                    RawResponse = CASE WHEN @RawResponse <> '' THEN @RawResponse ELSE RawResponse END,
                    ErrorMessage = '',
                    FailureClassification = NULL,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", optimisation.Id);
            command.Parameters.AddWithValue("@Completed", (int)PromptOptimisationStatus.Completed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(optimisation.CompletedUtc ?? DateTime.UtcNow));
            command.Parameters.AddWithValue("@SystemPromptRecommendationJson", optimisation.SystemPromptRecommendationJson ?? string.Empty);
            command.Parameters.AddWithValue("@ReviewPromptRecommendationJson", optimisation.ReviewPromptRecommendationJson ?? string.Empty);
            command.Parameters.AddWithValue("@RawResponse", optimisation.RawResponse ?? string.Empty);
            AddUsageParameters(command, optimisation.PromptTokens, optimisation.CompletionTokens, optimisation.TotalTokens);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Marks the optimisation failed.</summary>
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
                UPDATE PromptOptimisations
                SET Status = @Failed,
                    CompletedUtc = @CompletedUtc,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    ErrorMessage = @ErrorMessage,
                    FailureClassification = @FailureClassification,
                    RawResponse = CASE WHEN @RawResponse <> '' THEN @RawResponse ELSE RawResponse END,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Failed", (int)PromptOptimisationStatus.Failed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(utcNow));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            command.Parameters.AddWithValue("@FailureClassification", (int)category);
            AddUsageParameters(command, rawResponse, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the optimisation to the pending state for a later retry.</summary>
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
                UPDATE PromptOptimisations
                SET Status = @Pending,
                    NextAttemptUtc = @NextAttemptUtc,
                    LeasedUntilUtc = NULL,
                    ErrorMessage = @ErrorMessage,
                    RawResponse = CASE WHEN @RawResponse <> '' THEN @RawResponse ELSE RawResponse END,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Pending", (int)PromptOptimisationStatus.Pending);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToText(nextAttemptUtc));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            AddUsageParameters(command, rawResponse, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddUsageParameters(SqliteCommand command, int promptTokens, int completionTokens, int totalTokens)
        {
            command.Parameters.AddWithValue("@PromptTokens", promptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", completionTokens);
            command.Parameters.AddWithValue("@TotalTokens", totalTokens);
        }

        private static void AddUsageParameters(SqliteCommand command, string rawResponse, TokenUsage usage)
        {
            TokenUsage safeUsage = usage ?? new TokenUsage();
            command.Parameters.AddWithValue("@RawResponse", rawResponse ?? string.Empty);
            command.Parameters.AddWithValue("@PromptTokens", safeUsage.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", safeUsage.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", safeUsage.TotalTokens);
        }

        private static async Task<PromptOptimisation> ReadSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
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

        private static PromptOptimisation Map(SqliteDataReader reader)
        {
            return new PromptOptimisation
            {
                Id = reader.GetInt64(0),
                IssueValidationId = reader.GetNullableInt64(1),
                FindingId = reader.GetInt64(2),
                RepositoryId = reader.GetInt64(3),
                ReviewTypeId = reader.GetNullableInt64(4),
                Status = (PromptOptimisationStatus)reader.GetInt32(5),
                RetryOfOptimisationId = reader.GetNullableInt64(6),
                AttemptCount = reader.GetInt32(7),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(8)),
                StartedUtc = ReadNullableDate(reader, 9),
                CompletedUtc = ReadNullableDate(reader, 10),
                NextAttemptUtc = ReadNullableDate(reader, 11),
                LeasedUntilUtc = ReadNullableDate(reader, 12),
                Provider = (AIProvider)reader.GetInt32(13),
                Model = reader.GetString(14),
                EngineConfigSnapshotJson = reader.GetString(15),
                OriginalSystemPrompt = reader.GetString(16),
                OriginalReviewPrompt = reader.GetString(17),
                OriginalSystemPromptVersionId = reader.GetNullableInt64(18),
                OriginalReviewPromptVersionId = reader.GetNullableInt64(19),
                AnalysisJson = reader.GetString(20),
                SystemPromptRecommendationJson = reader.GetString(21),
                ReviewPromptRecommendationJson = reader.GetString(22),
                RawResponse = reader.GetString(23),
                ErrorMessage = reader.GetString(24),
                FailureClassification = reader.IsDBNull(25) ? null : (FailureCategory)reader.GetInt32(25),
                PromptTokens = reader.GetInt32(26),
                CompletionTokens = reader.GetInt32(27),
                TotalTokens = reader.GetInt32(28)
            };
        }
    }
}
