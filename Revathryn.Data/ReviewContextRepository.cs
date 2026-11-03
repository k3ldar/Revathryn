using System.Text.Json;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IReviewContextRepository"/>.</summary>
    public sealed class ReviewContextRepository : SqliteRepositoryBase, IReviewContextRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RepoFileId, ReviewTypeId, Sha256, Language, FilePath, ReviewInstructions, FileContent, ContextItemsJson, CreatedUtc, ReviewPrompt, SystemPrompt, ReviewDefinitionVersion, EngineName, EngineProvider, EngineModel, RequestedContextKeysJson, SystemPromptVersionId, ReviewPromptVersionId";

        /// <summary>Initialises a new repository.</summary>
        public ReviewContextRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the context with the given id, or null when not found.</summary>
        public async Task<AIReviewContext> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewContexts WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapContext(reader);
            }

            return null;
        }

        /// <summary>Inserts the context and returns its assigned id.</summary>
        public async Task<long> AddAsync(AIReviewContext context, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await AddAsync(connection, null, context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts the context within the supplied transaction and returns its assigned id.</summary>
        internal static async Task<long> AddAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            AIReviewContext context,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ReviewContexts (RepositoryId, RepoFileId, ReviewTypeId, Sha256, Language, FilePath, ReviewInstructions, FileContent, ContextItemsJson, CreatedUtc, ReviewPrompt, SystemPrompt, ReviewDefinitionVersion, EngineName, EngineProvider, EngineModel, RequestedContextKeysJson, SystemPromptVersionId, ReviewPromptVersionId)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @Sha256, @Language, @FilePath, @ReviewInstructions, @FileContent, @ContextItemsJson, @CreatedUtc, @ReviewPrompt, @SystemPrompt, @ReviewDefinitionVersion, @EngineName, @EngineProvider, @EngineModel, @RequestedContextKeysJson, @SystemPromptVersionId, @ReviewPromptVersionId);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, context);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        private static void AddParameters(SqliteCommand command, AIReviewContext context)
        {
            command.Parameters.AddWithValue("@RepositoryId", context.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", context.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", context.ReviewTypeId);
            command.Parameters.AddWithValue("@Sha256", context.Sha256);
            command.Parameters.AddWithValue("@Language", context.Language);
            command.Parameters.AddWithValue("@FilePath", context.FilePath);
            command.Parameters.AddWithValue("@ReviewInstructions", context.ReviewInstructions);
            command.Parameters.AddWithValue("@FileContent", context.FileContent);
            command.Parameters.AddWithValue("@ContextItemsJson", SerializeContextItems(context.ContextItems));
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(context.CreatedUtc));
            command.Parameters.AddWithValue("@ReviewPrompt", context.ReviewPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@SystemPrompt", context.SystemPrompt ?? string.Empty);
            command.Parameters.AddWithValue("@ReviewDefinitionVersion", context.ReviewDefinitionVersion ?? string.Empty);
            command.Parameters.AddWithValue("@EngineName", context.EngineName ?? string.Empty);
            command.Parameters.AddWithValue("@EngineProvider", context.EngineProvider ?? string.Empty);
            command.Parameters.AddWithValue("@EngineModel", context.EngineModel ?? string.Empty);
            command.Parameters.AddWithValue("@RequestedContextKeysJson", SerializeKeys(context.RequestedContextKeys));
            command.Parameters.AddWithValue("@SystemPromptVersionId", SqliteValueConverter.ToParameter(context.SystemPromptVersionId));
            command.Parameters.AddWithValue("@ReviewPromptVersionId", SqliteValueConverter.ToParameter(context.ReviewPromptVersionId));
        }

        private static string SerializeKeys(IReadOnlyList<string> keys)
        {
            return keys is null || keys.Count == 0 ? string.Empty : JsonSerializer.Serialize(keys);
        }

        private static IReadOnlyList<string> DeserializeKeys(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static string SerializeContextItems(IReadOnlyList<ReviewContextItem> items)
        {
            return items is null || items.Count == 0 ? string.Empty : JsonSerializer.Serialize(items);
        }

        private static IReadOnlyList<ReviewContextItem> DeserializeContextItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<ReviewContextItem>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static AIReviewContext MapContext(SqliteDataReader reader)
        {
            return new AIReviewContext
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                ReviewTypeId = reader.GetInt64(3),
                Sha256 = reader.GetString(4),
                Language = reader.GetString(5),
                FilePath = reader.GetString(6),
                ReviewInstructions = reader.GetString(7),
                FileContent = reader.GetString(8),
                ContextItems = DeserializeContextItems(reader.GetString(9)),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(10)),
                ReviewPrompt = reader.GetString(11),
                SystemPrompt = reader.GetString(12),
                ReviewDefinitionVersion = reader.GetString(13),
                EngineName = reader.GetString(14),
                EngineProvider = reader.GetString(15),
                EngineModel = reader.GetString(16),
                RequestedContextKeys = DeserializeKeys(reader.GetString(17)),
                SystemPromptVersionId = reader.GetNullableInt64(18),
                ReviewPromptVersionId = reader.GetNullableInt64(19)
            };
        }
    }
}
