using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IAISettingsRepository"/>.</summary>
    public sealed class AISettingsRepository : SqliteRepositoryBase, IAISettingsRepository
    {
        /// <summary>Initialises a new repository.</summary>
        public AISettingsRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the global AI settings, creating defaults when absent.</summary>
        public async Task<AISettings> GetAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand selectCommand = connection.CreateCommand())
            {
                selectCommand.CommandText = "SELECT MasterPrompt, LastModifiedDate FROM AISettings WHERE Id = 1 LIMIT 1;";

                using SqliteDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return MapAISettings(reader);
                }
            }

            AISettings defaults = new()
            {
                MasterPrompt = string.Empty,
                LastModifiedDate = DateTime.UtcNow
            };

            await UpdateAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        /// <summary>Persists the global AI settings.</summary>
        public async Task UpdateAsync(AISettings settings, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AISettings (Id, MasterPrompt, LastModifiedDate)
                VALUES (1, @MasterPrompt, @LastModifiedDate)
                ON CONFLICT (Id) DO UPDATE SET
                    MasterPrompt = excluded.MasterPrompt,
                    LastModifiedDate = excluded.LastModifiedDate;
                """;
            command.Parameters.AddWithValue("@MasterPrompt", settings.MasterPrompt);
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(settings.LastModifiedDate));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static AISettings MapAISettings(SqliteDataReader reader)
        {
            return new AISettings
            {
                MasterPrompt = reader.GetString(0),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(1))
            };
        }
    }
}
