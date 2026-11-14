using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IAIEngineRepository"/>.</summary>
    public sealed class AIEngineRepository : SqliteRepositoryBase, IAIEngineRepository
    {
        private const string SelectColumns = "Id, Name, Provider, Endpoint, Model, SecretKeyName, Enabled, IsDefault, MaxTokens, UseArchitecturePass, MaxSimultaneousConnections";

        /// <summary>Initialises a new repository.</summary>
        public AIEngineRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the engine with the given id, or null when not found.</summary>
        public async Task<AIEngine> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM AIEngines WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapAIEngine(reader);
            }

            return null;
        }

        /// <summary>Returns all configured engines.</summary>
        public async Task<IReadOnlyList<AIEngine>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM AIEngines ORDER BY Name ASC;";

            List<AIEngine> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapAIEngine(reader));
            }

            return results;
        }

        /// <summary>Returns all enabled engines.</summary>
        public async Task<IReadOnlyList<AIEngine>> GetEnabledAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM AIEngines WHERE Enabled = 1 ORDER BY Name ASC;";

            List<AIEngine> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapAIEngine(reader));
            }

            return results;
        }

        /// <summary>Returns the default engine, or null when none is configured.</summary>
        public async Task<AIEngine> GetDefaultAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM AIEngines WHERE IsDefault = 1 LIMIT 1;";

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapAIEngine(reader);
            }

            return null;
        }

        /// <summary>Inserts the engine and returns its assigned id.</summary>
        public async Task<long> AddAsync(AIEngine engine, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AIEngines (Name, Provider, Endpoint, Model, SecretKeyName, Enabled, IsDefault, MaxTokens, UseArchitecturePass, MaxSimultaneousConnections)
                VALUES (@Name, @Provider, @Endpoint, @Model, @SecretKeyName, @Enabled, @IsDefault, @MaxTokens, @UseArchitecturePass, @MaxSimultaneousConnections);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, engine);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an existing engine.</summary>
        public async Task UpdateAsync(AIEngine engine, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE AIEngines
                SET Name = @Name,
                    Provider = @Provider,
                    Endpoint = @Endpoint,
                    Model = @Model,
                    SecretKeyName = @SecretKeyName,
                    Enabled = @Enabled,
                    IsDefault = @IsDefault,
                    MaxTokens = @MaxTokens,
                    UseArchitecturePass = @UseArchitecturePass,
                    MaxSimultaneousConnections = @MaxSimultaneousConnections
                WHERE Id = @Id;
                """;
            AddParameters(command, engine);
            command.Parameters.AddWithValue("@Id", engine.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the engine with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM AIEngines WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, AIEngine engine)
        {
            command.Parameters.AddWithValue("@Name", engine.Name);
            command.Parameters.AddWithValue("@Provider", (int)engine.Provider);
            command.Parameters.AddWithValue("@Endpoint", engine.Endpoint);
            command.Parameters.AddWithValue("@Model", engine.Model);
            command.Parameters.AddWithValue("@SecretKeyName", engine.SecretKeyName);
            command.Parameters.AddWithValue("@Enabled", engine.Enabled);
            command.Parameters.AddWithValue("@IsDefault", engine.IsDefault);
            command.Parameters.AddWithValue("@MaxTokens", engine.MaxTokens);
            command.Parameters.AddWithValue("@UseArchitecturePass", engine.UseArchitecturePass);
            command.Parameters.AddWithValue("@MaxSimultaneousConnections", engine.MaxSimultaneousConnections);
        }

        private static AIEngine MapAIEngine(SqliteDataReader reader)
        {
            return new AIEngine
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Provider = (AIProvider)reader.GetInt32(2),
                Endpoint = reader.GetString(3),
                Model = reader.GetString(4),
                SecretKeyName = reader.GetString(5),
                Enabled = reader.GetBoolean(6),
                IsDefault = reader.GetBoolean(7),
                MaxTokens = reader.GetInt32(8),
                UseArchitecturePass = reader.GetBoolean(9),
                MaxSimultaneousConnections = reader.GetInt32(10)
            };
        }
    }
}
