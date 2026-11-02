using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IReviewEngineSettingsRepository"/>.</summary>
    public sealed class ReviewEngineSettingsRepository : SqliteRepositoryBase, IReviewEngineSettingsRepository
    {
        private const string SelectColumns =
            "Provider, Endpoint, Model, Temperature, ReasoningEffort, MaxTokens, SecretKeyName, Enabled, AdditionalOptions, LastModifiedUtc";

        /// <summary>Initialises a new repository.</summary>
        public ReviewEngineSettingsRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the saved settings, or null when none have been saved.</summary>
        public async Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewEngineSettings WHERE Id = 1 LIMIT 1;";

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        /// <summary>Inserts or replaces the settings.</summary>
        public async Task SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ReviewEngineSettings (Id, Provider, Endpoint, Model, Temperature, ReasoningEffort, MaxTokens, SecretKeyName, Enabled, AdditionalOptions, LastModifiedUtc)
                VALUES (1, @Provider, @Endpoint, @Model, @Temperature, @ReasoningEffort, @MaxTokens, @SecretKeyName, @Enabled, @AdditionalOptions, @LastModifiedUtc)
                ON CONFLICT (Id) DO UPDATE SET
                    Provider = excluded.Provider,
                    Endpoint = excluded.Endpoint,
                    Model = excluded.Model,
                    Temperature = excluded.Temperature,
                    ReasoningEffort = excluded.ReasoningEffort,
                    MaxTokens = excluded.MaxTokens,
                    SecretKeyName = excluded.SecretKeyName,
                    Enabled = excluded.Enabled,
                    AdditionalOptions = excluded.AdditionalOptions,
                    LastModifiedUtc = excluded.LastModifiedUtc;
                """;
            command.Parameters.AddWithValue("@Provider", (int)settings.Provider);
            command.Parameters.AddWithValue("@Endpoint", settings.Endpoint ?? string.Empty);
            command.Parameters.AddWithValue("@Model", settings.Model ?? string.Empty);
            command.Parameters.AddWithValue("@Temperature", settings.Temperature.HasValue ? settings.Temperature.Value : DBNull.Value);
            command.Parameters.AddWithValue("@ReasoningEffort", settings.ReasoningEffort ?? string.Empty);
            command.Parameters.AddWithValue("@MaxTokens", settings.MaxTokens);
            command.Parameters.AddWithValue("@SecretKeyName", settings.SecretKeyName ?? string.Empty);
            command.Parameters.AddWithValue("@Enabled", settings.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("@AdditionalOptions", settings.AdditionalOptions ?? string.Empty);
            command.Parameters.AddWithValue("@LastModifiedUtc", SqliteValueConverter.ToText(settings.LastModifiedUtc));

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static ReviewEngineSettings Map(SqliteDataReader reader)
        {
            return new ReviewEngineSettings
            {
                Provider = (AIProvider)reader.GetInt32(0),
                Endpoint = reader.GetString(1),
                Model = reader.GetString(2),
                Temperature = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                ReasoningEffort = reader.GetString(4),
                MaxTokens = reader.GetInt32(5),
                SecretKeyName = reader.GetString(6),
                Enabled = reader.GetInt32(7) != 0,
                AdditionalOptions = reader.GetString(8),
                LastModifiedUtc = SqliteValueConverter.ToDateTime(reader.GetString(9))
            };
        }
    }
}
