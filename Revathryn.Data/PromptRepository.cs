using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IPromptRepository"/>.</summary>
    public sealed class PromptRepository : SqliteRepositoryBase, IPromptRepository
    {
        private const string SelectColumns = "Id, PromptType, ReviewTypeId, CreatedUtc, LastModifiedUtc";

        /// <summary>Initialises a new repository.</summary>
        public PromptRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns a prompt identity by id, or null when not found.</summary>
        public async Task<Prompt> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Prompts WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the single system prompt identity, or null when not seeded.</summary>
        public async Task<Prompt> GetSystemAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Prompts WHERE PromptType = @System LIMIT 1;";
            command.Parameters.AddWithValue("@System", (int)PromptType.System);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the prompt identity for a review type, or null when not seeded.</summary>
        public async Task<Prompt> GetByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Prompts WHERE PromptType = @Review AND ReviewTypeId = @ReviewTypeId LIMIT 1;";
            command.Parameters.AddWithValue("@Review", (int)PromptType.Review);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns every prompt identity, ordered by type then review type id.</summary>
        public async Task<IReadOnlyList<Prompt>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Prompts ORDER BY PromptType ASC, ReviewTypeId ASC;";

            List<Prompt> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Inserts a prompt identity and returns its assigned id.</summary>
        public async Task<long> AddAsync(Prompt prompt, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prompt);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Prompts (PromptType, ReviewTypeId, CreatedUtc, LastModifiedUtc)
                VALUES (@PromptType, @ReviewTypeId, @CreatedUtc, @LastModifiedUtc);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@PromptType", (int)prompt.PromptType);
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(prompt.ReviewTypeId));
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(prompt.CreatedUtc));
            command.Parameters.AddWithValue("@LastModifiedUtc", SqliteValueConverter.ToText(prompt.LastModifiedUtc));

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates the last-modified timestamp of an existing prompt identity.</summary>
        public async Task TouchAsync(long id, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE Prompts SET LastModifiedUtc = @LastModifiedUtc WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@LastModifiedUtc", SqliteValueConverter.ToText(utcNow));

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<Prompt> ReadSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
        {
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        private static Prompt Map(SqliteDataReader reader)
        {
            return new Prompt
            {
                Id = reader.GetInt64(0),
                PromptType = (PromptType)reader.GetInt32(1),
                ReviewTypeId = reader.GetNullableInt64(2),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(3)),
                LastModifiedUtc = SqliteValueConverter.ToDateTime(reader.GetString(4))
            };
        }
    }
}
