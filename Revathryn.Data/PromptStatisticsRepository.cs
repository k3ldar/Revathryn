using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IPromptStatisticsRepository"/>.</summary>
    public sealed class PromptStatisticsRepository : SqliteRepositoryBase, IPromptStatisticsRepository
    {
        private const string AndSeparator = " AND ";
        private const string WherePrefix = " WHERE ";
        private const string PromptVersionSystemOrReviewCondition = "(c.SystemPromptVersionId = @PromptVersionId OR c.ReviewPromptVersionId = @PromptVersionId)";
        private const string LatestCompletedValidationCondition = "v.Id = (SELECT v2.Id FROM IssueValidations v2 WHERE v2.FindingId = v.FindingId AND v2.Status = @Completed ORDER BY v2.CreatedUtc DESC, v2.Id DESC LIMIT 1)";

        private const string ReviewTypeIdParameter = "@ReviewTypeId";
        private const string PromptVersionIdParameter = "@PromptVersionId";
        private const string DetectionModelParameter = "@DetectionModel";
        private const string FromUtcParameter = "@FromUtc";
        private const string ToUtcParameter = "@ToUtc";
        private const string CompletedParameter = "@Completed";
        private const string AgreedParameter = "@Agreed";
        private const string DisagreedParameter = "@Disagreed";
        private const string PartiallyAgreedParameter = "@PartiallyAgreed";
        private const string InconclusiveParameter = "@Inconclusive";

        /// <summary>Initialises a new repository over the supplied connection factory.</summary>
        public PromptStatisticsRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the raw counts matching the supplied filter.</summary>
        public async Task<PromptStatisticsSnapshot> GetSnapshotAsync(PromptStatisticsFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            PromptStatisticsSnapshot snapshot = new()
            {
                IssuesDetected = await CountIssuesAsync(connection, filter, cancellationToken).ConfigureAwait(false),
                PromptTests = await CountTestRunsAsync(connection, filter, cancellationToken).ConfigureAwait(false),
                PromptActivations = await CountActivationsAsync(connection, filter, cancellationToken).ConfigureAwait(false)
            };

            ValidationCounts validationCounts = await CountValidationsAsync(connection, filter, cancellationToken).ConfigureAwait(false);

            snapshot.ValidatedIssues = validationCounts.ValidatedIssues;
            snapshot.AgreedCount = validationCounts.AgreedCount;
            snapshot.DisagreedCount = validationCounts.DisagreedCount;
            snapshot.PartiallyAgreedCount = validationCounts.PartiallyAgreedCount;
            snapshot.InconclusiveCount = validationCounts.InconclusiveCount;

            return snapshot;
        }

        private static async Task<int> CountIssuesAsync(SqliteConnection connection, PromptStatisticsFilter filter, CancellationToken cancellationToken)
        {
            List<string> conditions = [];
            List<(string Name, object Value)> parameters = [];

            AddFilterConditions(conditions, parameters, filter, "f.ReviewTypeId", PromptVersionSystemOrReviewCondition, "c.EngineModel", "f.CreatedDate");

            string sql = $"SELECT COUNT(*) FROM Findings f LEFT JOIN ReviewContexts c ON f.ReviewContextId = c.Id{BuildWhereClause(conditions)};";

            return await ExecuteCountAsync(connection, sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<ValidationCounts> CountValidationsAsync(SqliteConnection connection, PromptStatisticsFilter filter, CancellationToken cancellationToken)
        {
            List<string> conditions = [];
            List<(string Name, object Value)> parameters = [];

            conditions.Add("v.Status = @Completed");
            conditions.Add(LatestCompletedValidationCondition);
            parameters.Add((CompletedParameter, (int)IssueValidationStatus.Completed));
            parameters.Add((AgreedParameter, (int)IssueValidationVerdict.Agreed));
            parameters.Add((DisagreedParameter, (int)IssueValidationVerdict.Disagreed));
            parameters.Add((PartiallyAgreedParameter, (int)IssueValidationVerdict.PartiallyAgreed));
            parameters.Add((InconclusiveParameter, (int)IssueValidationVerdict.Inconclusive));

            AddFilterConditions(conditions, parameters, filter, "f.ReviewTypeId", PromptVersionSystemOrReviewCondition, "c.EngineModel", "v.CreatedUtc");

            string sql = $"""
                SELECT COUNT(*),
                       COALESCE(SUM(CASE WHEN v.Verdict = @Agreed THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN v.Verdict = @Disagreed THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN v.Verdict = @PartiallyAgreed THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN v.Verdict = @Inconclusive THEN 1 ELSE 0 END), 0)
                FROM IssueValidations v
                JOIN Findings f ON v.FindingId = f.Id
                LEFT JOIN ReviewContexts c ON f.ReviewContextId = c.Id
                {BuildWhereClause(conditions)};
                """;

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameters(command, parameters);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ValidationCounts
                {
                    ValidatedIssues = reader.GetInt32(0),
                    AgreedCount = reader.GetInt32(1),
                    DisagreedCount = reader.GetInt32(2),
                    PartiallyAgreedCount = reader.GetInt32(3),
                    InconclusiveCount = reader.GetInt32(4)
                };
            }

            return new ValidationCounts();
        }

        private static async Task<int> CountTestRunsAsync(SqliteConnection connection, PromptStatisticsFilter filter, CancellationToken cancellationToken)
        {
            List<string> conditions = [];
            List<(string Name, object Value)> parameters = [];

            AddFilterConditions(conditions, parameters, filter, "t.ReviewTypeId", "t.PromptVersionId = @PromptVersionId", "t.DetectionModel", "t.CreatedUtc");

            string sql = $"SELECT COUNT(*) FROM PromptTestRuns t{BuildWhereClause(conditions)};";

            return await ExecuteCountAsync(connection, sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> CountActivationsAsync(SqliteConnection connection, PromptStatisticsFilter filter, CancellationToken cancellationToken)
        {
            List<string> conditions = [];
            List<(string Name, object Value)> parameters = [];

            conditions.Add("pv.ActivatedUtc IS NOT NULL");
            AddFilterConditions(conditions, parameters, filter, "p.ReviewTypeId", "pv.Id = @PromptVersionId", string.Empty, "pv.ActivatedUtc");

            string sql = $"SELECT COUNT(*) FROM PromptVersions pv JOIN Prompts p ON pv.PromptId = p.Id{BuildWhereClause(conditions)};";

            return await ExecuteCountAsync(connection, sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> ExecuteCountAsync(
            SqliteConnection connection,
            string sql,
            List<(string Name, object Value)> parameters,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameters(command, parameters);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        private static void AddFilterConditions(
            List<string> conditions,
            List<(string Name, object Value)> parameters,
            PromptStatisticsFilter filter,
            string reviewTypeColumn,
            string promptVersionCondition,
            string detectionModelColumn,
            string dateColumn)
        {
            if (filter.ReviewTypeId.HasValue)
            {
                conditions.Add($"{reviewTypeColumn} = {ReviewTypeIdParameter}");
                parameters.Add((ReviewTypeIdParameter, filter.ReviewTypeId.Value));
            }

            if (filter.PromptVersionId.HasValue)
            {
                conditions.Add(promptVersionCondition);
                parameters.Add((PromptVersionIdParameter, filter.PromptVersionId.Value));
            }

            string detectionModel = Normalise(filter.DetectionModel);

            if (detectionModelColumn.Length > 0 && detectionModel.Length > 0)
            {
                conditions.Add($"{detectionModelColumn} = {DetectionModelParameter}");
                parameters.Add((DetectionModelParameter, detectionModel));
            }

            if (filter.FromUtc.HasValue)
            {
                conditions.Add($"{dateColumn} >= {FromUtcParameter}");
                parameters.Add((FromUtcParameter, SqliteValueConverter.ToText(filter.FromUtc.Value)));
            }

            if (filter.ToUtc.HasValue)
            {
                conditions.Add($"{dateColumn} <= {ToUtcParameter}");
                parameters.Add((ToUtcParameter, SqliteValueConverter.ToText(filter.ToUtc.Value)));
            }
        }

        private static void AddParameters(SqliteCommand command, List<(string Name, object Value)> parameters)
        {
            foreach ((string name, object value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        private static string BuildWhereClause(List<string> conditions)
        {
            if (conditions.Count == 0)
            {
                return string.Empty;
            }

            return $"{WherePrefix}{string.Join(AndSeparator, conditions)}";
        }

        private static string Normalise(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private sealed class ValidationCounts
        {
            public int ValidatedIssues { get; set; }

            public int AgreedCount { get; set; }

            public int DisagreedCount { get; set; }

            public int PartiallyAgreedCount { get; set; }

            public int InconclusiveCount { get; set; }
        }
    }
}
