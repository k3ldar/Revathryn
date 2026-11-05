using Microsoft.Data.Sqlite;

using Revathryn.Contracts;
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
                selectCommand.CommandText = "SELECT MasterPrompt, LastModifiedDate, IssueSimilarityEnabled, IssueSimilarityThreshold, IssueSimilarityMinimumTokenLength, IssueDefaultPageSize, IssuePageSizeOptions FROM AISettings WHERE Id = 1 LIMIT 1;";

                using SqliteDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return MapAISettings(reader);
                }
            }

            AISettings defaults = new()
            {
                MasterPrompt = string.Empty,
                LastModifiedDate = DateTime.UtcNow,
                IssueSimilarityEnabled = true,
                IssueSimilarityThreshold = Consts.DefaultIssueSimilarityThreshold,
                IssueSimilarityMinimumTokenLength = Consts.DefaultIssueSimilarityMinimumTokenLength,
                IssueDefaultPageSize = Consts.IssueDefaultPageSize,
                IssuePageSizeOptions = IssuePageSizeOptionsFormatter.Format(new IssueViewingOptions().PageSizeOptions)
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
                INSERT INTO AISettings (Id, MasterPrompt, LastModifiedDate, IssueSimilarityEnabled, IssueSimilarityThreshold, IssueSimilarityMinimumTokenLength, IssueDefaultPageSize, IssuePageSizeOptions)
                VALUES (1, @MasterPrompt, @LastModifiedDate, @IssueSimilarityEnabled, @IssueSimilarityThreshold, @IssueSimilarityMinimumTokenLength, @IssueDefaultPageSize, @IssuePageSizeOptions)
                ON CONFLICT (Id) DO UPDATE SET
                    MasterPrompt = excluded.MasterPrompt,
                    LastModifiedDate = excluded.LastModifiedDate,
                    IssueSimilarityEnabled = excluded.IssueSimilarityEnabled,
                    IssueSimilarityThreshold = excluded.IssueSimilarityThreshold,
                    IssueSimilarityMinimumTokenLength = excluded.IssueSimilarityMinimumTokenLength,
                    IssueDefaultPageSize = excluded.IssueDefaultPageSize,
                    IssuePageSizeOptions = excluded.IssuePageSizeOptions;
                """;
            command.Parameters.AddWithValue("@MasterPrompt", settings.MasterPrompt);
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(settings.LastModifiedDate));
            command.Parameters.AddWithValue("@IssueSimilarityEnabled", settings.IssueSimilarityEnabled);
            command.Parameters.AddWithValue("@IssueSimilarityThreshold", settings.IssueSimilarityThreshold);
            command.Parameters.AddWithValue("@IssueSimilarityMinimumTokenLength", settings.IssueSimilarityMinimumTokenLength);
            command.Parameters.AddWithValue("@IssueDefaultPageSize", settings.IssueDefaultPageSize);
            command.Parameters.AddWithValue("@IssuePageSizeOptions", settings.IssuePageSizeOptions);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static AISettings MapAISettings(SqliteDataReader reader)
        {
            return new AISettings
            {
                MasterPrompt = reader.GetString(0),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(1)),
                IssueSimilarityEnabled = reader.GetBoolean(2),
                IssueSimilarityThreshold = reader.GetDouble(3),
                IssueSimilarityMinimumTokenLength = reader.GetInt32(4),
                IssueDefaultPageSize = reader.GetInt32(5),
                IssuePageSizeOptions = reader.GetString(6)
            };
        }
    }
}
