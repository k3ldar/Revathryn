using System.Globalization;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IIssueValidationRepository"/>.</summary>
    public sealed class IssueValidationRepository : SqliteRepositoryBase, IIssueValidationRepository
    {
        private const int SqliteConstraintUniqueExtendedCode = 2067;
        private const string ParameterPrefix = "@F";

        private const string SelectColumns =
            "Id, FindingId, RepositoryId, Status, RetryOfValidationId, AttemptCount, CreatedUtc, StartedUtc, CompletedUtc, NextAttemptUtc, LeasedUntilUtc, " +
            "Provider, Model, EngineConfigSnapshotJson, PromptVersion, SystemPrompt, UserPrompt, ContextReferenceJson, " +
            "OriginalSeverity, OriginalTitle, OriginalDescription, OriginalSuggestedFix, " +
            "Verdict, Confidence, Summary, Reasoning, RecommendedSeverity, SeverityReasoning, EvidenceJson, SuggestionsJson, LimitationsJson, " +
            "RawResponse, ErrorMessage, FailureClassification, PromptTokens, CompletionTokens, TotalTokens";

        // Same ordinals as SelectColumns, with the large diagnostic columns omitted.
        private const string SummaryColumns =
            "Id, FindingId, RepositoryId, Status, RetryOfValidationId, AttemptCount, CreatedUtc, StartedUtc, CompletedUtc, NextAttemptUtc, LeasedUntilUtc, " +
            "Provider, Model, '' AS EngineConfigSnapshotJson, PromptVersion, '' AS SystemPrompt, '' AS UserPrompt, '' AS ContextReferenceJson, " +
            "OriginalSeverity, OriginalTitle, '' AS OriginalDescription, '' AS OriginalSuggestedFix, " +
            "Verdict, Confidence, Summary, '' AS Reasoning, RecommendedSeverity, '' AS SeverityReasoning, '' AS EvidenceJson, '' AS SuggestionsJson, '' AS LimitationsJson, " +
            "'' AS RawResponse, '' AS ErrorMessage, FailureClassification, PromptTokens, CompletionTokens, TotalTokens";

        /// <summary>Initialises a new repository.</summary>
        public IssueValidationRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a pending validation, or returns 0 when the finding already has an active one.</summary>
        public async Task<long> TryAddAsync(IssueValidation validation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(validation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO IssueValidations (
                    FindingId, RepositoryId, Status, RetryOfValidationId, AttemptCount, CreatedUtc, Provider, Model,
                    OriginalSeverity, OriginalTitle, OriginalDescription, OriginalSuggestedFix)
                VALUES (
                    @FindingId, @RepositoryId, @Status, @RetryOfValidationId, 0, @CreatedUtc, @Provider, @Model,
                    @OriginalSeverity, @OriginalTitle, @OriginalDescription, @OriginalSuggestedFix);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@FindingId", validation.FindingId);
            command.Parameters.AddWithValue("@RepositoryId", validation.RepositoryId);
            command.Parameters.AddWithValue("@Status", (int)IssueValidationStatus.Pending);
            command.Parameters.AddWithValue("@RetryOfValidationId", SqliteValueConverter.ToParameter(validation.RetryOfValidationId));
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(validation.CreatedUtc));
            command.Parameters.AddWithValue("@Provider", (int)validation.Provider);
            command.Parameters.AddWithValue("@Model", validation.Model ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalSeverity", (int)validation.OriginalSeverity);
            command.Parameters.AddWithValue("@OriginalTitle", validation.OriginalTitle ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalDescription", validation.OriginalDescription ?? string.Empty);
            command.Parameters.AddWithValue("@OriginalSuggestedFix", validation.OriginalSuggestedFix ?? string.Empty);

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

        /// <summary>Returns a validation by id, or null.</summary>
        public async Task<IssueValidation> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM IssueValidations WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns all validations for a finding, newest first.</summary>
        public async Task<IReadOnlyList<IssueValidation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM IssueValidations WHERE FindingId = @FindingId ORDER BY CreatedUtc DESC, Id DESC;";
            command.Parameters.AddWithValue("@FindingId", findingId);

            List<IssueValidation> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Returns the most recent validation for a finding, regardless of status, or null.</summary>
        public async Task<IssueValidation> GetLatestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM IssueValidations WHERE FindingId = @FindingId ORDER BY CreatedUtc DESC, Id DESC LIMIT 1;";
            command.Parameters.AddWithValue("@FindingId", findingId);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the pending or running validation for a finding, or null.</summary>
        public async Task<IssueValidation> GetActiveByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM IssueValidations WHERE FindingId = @FindingId AND Status IN (@Pending, @Running) LIMIT 1;";
            command.Parameters.AddWithValue("@FindingId", findingId);
            command.Parameters.AddWithValue("@Pending", (int)IssueValidationStatus.Pending);
            command.Parameters.AddWithValue("@Running", (int)IssueValidationStatus.Running);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the most recent completed validation for each finding, as a summary projection.</summary>
        public async Task<IReadOnlyDictionary<long, IssueValidation>> GetLatestCompletedByFindingsAsync(
            IReadOnlyCollection<long> findingIds,
            CancellationToken cancellationToken = default)
        {
            Dictionary<long, IssueValidation> results = [];

            if (findingIds is null || findingIds.Count == 0)
            {
                return results;
            }

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();

            List<string> names = [];
            int index = 0;

            foreach (long findingId in findingIds.Distinct())
            {
                string name = string.Concat(ParameterPrefix, index.ToString(CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue(name, findingId);
                names.Add(name);
                index++;
            }

            command.CommandText = $"""
                SELECT {SummaryColumns}
                FROM IssueValidations
                WHERE Status = @Completed
                  AND FindingId IN ({string.Join(",", names)})
                  AND Id = (
                      SELECT v.Id FROM IssueValidations v
                      WHERE v.FindingId = IssueValidations.FindingId AND v.Status = @Completed
                      ORDER BY v.CreatedUtc DESC, v.Id DESC
                      LIMIT 1);
                """;
            command.Parameters.AddWithValue("@Completed", (int)IssueValidationStatus.Completed);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                IssueValidation validation = Map(reader);
                results[validation.FindingId] = validation;
            }

            return results;
        }

        /// <summary>Claims the next due validation under a lease, reclaiming any whose lease expired.</summary>
        public async Task<IssueValidation> ClaimNextAsync(DateTime utcNow, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            string now = SqliteValueConverter.ToText(utcNow);
            string lease = SqliteValueConverter.ToText(utcNow.Add(leaseDuration));

            using (SqliteCommand reclaim = connection.CreateCommand())
            {
                reclaim.Transaction = transaction;
                reclaim.CommandText = """
                    UPDATE IssueValidations
                    SET Status = @Pending, LeasedUntilUtc = NULL
                    WHERE Status = @Running AND LeasedUntilUtc < @Now;
                    """;
                reclaim.Parameters.AddWithValue("@Pending", (int)IssueValidationStatus.Pending);
                reclaim.Parameters.AddWithValue("@Running", (int)IssueValidationStatus.Running);
                reclaim.Parameters.AddWithValue("@Now", now);
                _ = await reclaim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using SqliteCommand claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = $"""
                UPDATE IssueValidations
                SET Status = @Running,
                    LeasedUntilUtc = @Lease,
                    AttemptCount = AttemptCount + 1,
                    StartedUtc = COALESCE(StartedUtc, @Now)
                WHERE Id = (
                    SELECT Id FROM IssueValidations
                    WHERE Status = @Pending AND (NextAttemptUtc IS NULL OR NextAttemptUtc <= @Now)
                    ORDER BY CreatedUtc ASC, Id ASC
                    LIMIT 1
                )
                RETURNING {SelectColumns};
                """;
            claim.Parameters.AddWithValue("@Running", (int)IssueValidationStatus.Running);
            claim.Parameters.AddWithValue("@Pending", (int)IssueValidationStatus.Pending);
            claim.Parameters.AddWithValue("@Lease", lease);
            claim.Parameters.AddWithValue("@Now", now);

            IssueValidation claimed = null;

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

        /// <summary>Records what is about to be sent to the review engine: engine, prompts and context.</summary>
        public async Task SaveExecutionSnapshotAsync(IssueValidation validation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(validation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE IssueValidations
                SET Provider = @Provider,
                    Model = @Model,
                    EngineConfigSnapshotJson = @EngineConfigSnapshotJson,
                    PromptVersion = @PromptVersion,
                    SystemPrompt = @SystemPrompt,
                    UserPrompt = @UserPrompt,
                    ContextReferenceJson = @ContextReferenceJson
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", validation.Id);
            command.Parameters.AddWithValue("@Provider", (int)validation.Provider);
            command.Parameters.AddWithValue("@Model", validation.Model ?? string.Empty);
            command.Parameters.AddWithValue("@EngineConfigSnapshotJson", validation.EngineConfigSnapshotJson ?? string.Empty);
            command.Parameters.AddWithValue("@PromptVersion", validation.PromptVersion ?? string.Empty);
            command.Parameters.AddWithValue("@SystemPrompt", validation.SystemPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@UserPrompt", validation.UserPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@ContextReferenceJson", validation.ContextReferenceJson ?? string.Empty);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Records the verdict and marks the validation completed.</summary>
        public async Task CompleteAsync(IssueValidation validation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(validation);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE IssueValidations
                SET Status = @Completed,
                    CompletedUtc = @CompletedUtc,
                    NextAttemptUtc = NULL,
                    LeasedUntilUtc = NULL,
                    Verdict = @Verdict,
                    Confidence = @Confidence,
                    Summary = @Summary,
                    Reasoning = @Reasoning,
                    RecommendedSeverity = @RecommendedSeverity,
                    SeverityReasoning = @SeverityReasoning,
                    EvidenceJson = @EvidenceJson,
                    SuggestionsJson = @SuggestionsJson,
                    LimitationsJson = @LimitationsJson,
                    RawResponse = @RawResponse,
                    ErrorMessage = '',
                    FailureClassification = NULL,
                    PromptTokens = PromptTokens + @PromptTokens,
                    CompletionTokens = CompletionTokens + @CompletionTokens,
                    TotalTokens = TotalTokens + @TotalTokens
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", validation.Id);
            command.Parameters.AddWithValue("@Completed", (int)IssueValidationStatus.Completed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(validation.CompletedUtc ?? DateTime.UtcNow));
            command.Parameters.AddWithValue("@Verdict", validation.Verdict.HasValue ? (int)validation.Verdict.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Confidence", validation.Confidence.HasValue ? validation.Confidence.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Summary", validation.Summary ?? string.Empty);
            command.Parameters.AddWithValue("@Reasoning", validation.Reasoning ?? string.Empty);
            command.Parameters.AddWithValue("@RecommendedSeverity", validation.RecommendedSeverity.HasValue ? (int)validation.RecommendedSeverity.Value : DBNull.Value);
            command.Parameters.AddWithValue("@SeverityReasoning", validation.SeverityReasoning ?? string.Empty);
            command.Parameters.AddWithValue("@EvidenceJson", validation.EvidenceJson ?? string.Empty);
            command.Parameters.AddWithValue("@SuggestionsJson", validation.SuggestionsJson ?? string.Empty);
            command.Parameters.AddWithValue("@LimitationsJson", validation.LimitationsJson ?? string.Empty);
            command.Parameters.AddWithValue("@RawResponse", validation.RawResponse ?? string.Empty);
            command.Parameters.AddWithValue("@PromptTokens", validation.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", validation.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", validation.TotalTokens);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Marks the validation failed. No verdict is recorded.</summary>
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
                UPDATE IssueValidations
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
            command.Parameters.AddWithValue("@Failed", (int)IssueValidationStatus.Failed);
            command.Parameters.AddWithValue("@CompletedUtc", SqliteValueConverter.ToText(utcNow));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            command.Parameters.AddWithValue("@FailureClassification", (int)category);
            AddUsageParameters(command, rawResponse, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the validation to the pending state for a later retry.</summary>
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
                UPDATE IssueValidations
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
            command.Parameters.AddWithValue("@Pending", (int)IssueValidationStatus.Pending);
            command.Parameters.AddWithValue("@NextAttemptUtc", SqliteValueConverter.ToText(nextAttemptUtc));
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? string.Empty);
            AddUsageParameters(command, rawResponse, usage);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddUsageParameters(SqliteCommand command, string rawResponse, TokenUsage usage)
        {
            TokenUsage safeUsage = usage ?? new TokenUsage();
            command.Parameters.AddWithValue("@RawResponse", rawResponse ?? string.Empty);
            command.Parameters.AddWithValue("@PromptTokens", safeUsage.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", safeUsage.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", safeUsage.TotalTokens);
        }

        private static async Task<IssueValidation> ReadSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
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

        private static IssueValidation Map(SqliteDataReader reader)
        {
            return new IssueValidation
            {
                Id = reader.GetInt64(0),
                FindingId = reader.GetInt64(1),
                RepositoryId = reader.GetInt64(2),
                Status = (IssueValidationStatus)reader.GetInt32(3),
                RetryOfValidationId = reader.GetNullableInt64(4),
                AttemptCount = reader.GetInt32(5),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(6)),
                StartedUtc = ReadNullableDate(reader, 7),
                CompletedUtc = ReadNullableDate(reader, 8),
                NextAttemptUtc = ReadNullableDate(reader, 9),
                LeasedUntilUtc = ReadNullableDate(reader, 10),
                Provider = (AIProvider)reader.GetInt32(11),
                Model = reader.GetString(12),
                EngineConfigSnapshotJson = reader.GetString(13),
                PromptVersion = reader.GetString(14),
                SystemPrompt = reader.GetString(15),
                UserPrompt = reader.GetString(16),
                ContextReferenceJson = reader.GetString(17),
                OriginalSeverity = (FindingSeverity)reader.GetInt32(18),
                OriginalTitle = reader.GetString(19),
                OriginalDescription = reader.GetString(20),
                OriginalSuggestedFix = reader.GetString(21),
                Verdict = reader.IsDBNull(22) ? null : (IssueValidationVerdict)reader.GetInt32(22),
                Confidence = reader.IsDBNull(23) ? null : reader.GetDouble(23),
                Summary = reader.GetString(24),
                Reasoning = reader.GetString(25),
                RecommendedSeverity = reader.IsDBNull(26) ? null : (FindingSeverity)reader.GetInt32(26),
                SeverityReasoning = reader.GetString(27),
                EvidenceJson = reader.GetString(28),
                SuggestionsJson = reader.GetString(29),
                LimitationsJson = reader.GetString(30),
                RawResponse = reader.GetString(31),
                ErrorMessage = reader.GetString(32),
                FailureClassification = reader.IsDBNull(33) ? null : (FailureCategory)reader.GetInt32(33),
                PromptTokens = reader.GetInt32(34),
                CompletionTokens = reader.GetInt32(35),
                TotalTokens = reader.GetInt32(36)
            };
        }
    }
}
