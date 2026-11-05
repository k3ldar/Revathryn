using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="ISeverityRuleRepository"/>.</summary>
    public sealed class SeverityRuleRepository : SqliteRepositoryBase, ISeverityRuleRepository
    {
        private const string SelectColumns =
            "Id, RuleOrder, Name, MatchText, MatchField, MatchSeverity, Action, TargetSeverity, Enabled, CreatedDate, LastModifiedDate";

        /// <summary>Initialises a new repository.</summary>
        public SeverityRuleRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the rule with the given id, or null when not found.</summary>
        public async Task<SeverityRule> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM SeverityRules WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapSeverityRule(reader);
            }

            return null;
        }

        /// <summary>Returns all rules in evaluation order.</summary>
        public async Task<IReadOnlyList<SeverityRule>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM SeverityRules ORDER BY RuleOrder ASC, Id ASC;";

            List<SeverityRule> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapSeverityRule(reader));
            }

            return results;
        }

        /// <summary>Returns all enabled rules in evaluation order.</summary>
        public async Task<IReadOnlyList<SeverityRule>> GetEnabledOrderedAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM SeverityRules WHERE Enabled = 1 ORDER BY RuleOrder ASC, Id ASC;";

            List<SeverityRule> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapSeverityRule(reader));
            }

            return results;
        }

        /// <summary>Inserts the rule and returns its assigned id.</summary>
        public async Task<long> AddAsync(SeverityRule rule, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SeverityRules (RuleOrder, Name, MatchText, MatchField, MatchSeverity, Action, TargetSeverity, Enabled, CreatedDate, LastModifiedDate)
                VALUES (@RuleOrder, @Name, @MatchText, @MatchField, @MatchSeverity, @Action, @TargetSeverity, @Enabled, @CreatedDate, @LastModifiedDate);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, rule);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an existing rule.</summary>
        public async Task UpdateAsync(SeverityRule rule, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE SeverityRules
                SET RuleOrder = @RuleOrder,
                    Name = @Name,
                    MatchText = @MatchText,
                    MatchField = @MatchField,
                    MatchSeverity = @MatchSeverity,
                    Action = @Action,
                    TargetSeverity = @TargetSeverity,
                    Enabled = @Enabled,
                    CreatedDate = @CreatedDate,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
                """;
            AddParameters(command, rule);
            command.Parameters.AddWithValue("@Id", rule.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the rule with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SeverityRules WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, SeverityRule rule)
        {
            command.Parameters.AddWithValue("@RuleOrder", rule.Order);
            command.Parameters.AddWithValue("@Name", rule.Name);
            command.Parameters.AddWithValue("@MatchText", rule.MatchText);
            command.Parameters.AddWithValue("@MatchField", (int)rule.MatchField);
            command.Parameters.AddWithValue("@MatchSeverity", rule.MatchSeverity.HasValue ? (object)(int)rule.MatchSeverity.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Action", (int)rule.Action);
            command.Parameters.AddWithValue("@TargetSeverity", (int)rule.TargetSeverity);
            command.Parameters.AddWithValue("@Enabled", rule.Enabled);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(rule.CreatedDate));
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(rule.LastModifiedDate));
        }

        private static SeverityRule MapSeverityRule(SqliteDataReader reader)
        {
            return new SeverityRule
            {
                Id = reader.GetInt64(0),
                Order = reader.GetInt32(1),
                Name = reader.GetString(2),
                MatchText = reader.GetString(3),
                MatchField = (SeverityRuleMatchField)reader.GetInt32(4),
                MatchSeverity = reader.IsDBNull(5) ? null : (FindingSeverity)reader.GetInt32(5),
                Action = (SeverityRuleAction)reader.GetInt32(6),
                TargetSeverity = (FindingSeverity)reader.GetInt32(7),
                Enabled = reader.GetBoolean(8),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(9)),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(10))
            };
        }
    }
}
