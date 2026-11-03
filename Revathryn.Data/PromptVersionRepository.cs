using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IPromptVersionRepository"/>.</summary>
    public sealed class PromptVersionRepository : SqliteRepositoryBase, IPromptVersionRepository
    {
        private const string SelectColumns =
            "Id, PromptId, VersionNumber, PromptText, Description, Origin, SourceOptimisationId, CreatedUtc, CreatedBy, IsActive, IsDraft, ActivatedUtc";

        /// <summary>Initialises a new repository.</summary>
        public PromptVersionRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns a prompt version by id, or null when not found.</summary>
        public async Task<PromptVersion> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptVersions WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns all versions for a prompt identity, newest first.</summary>
        public async Task<IReadOnlyList<PromptVersion>> GetByPromptAsync(long promptId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptVersions WHERE PromptId = @PromptId ORDER BY VersionNumber DESC, Id DESC;";
            command.Parameters.AddWithValue("@PromptId", promptId);

            List<PromptVersion> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        /// <summary>Returns the active version for a prompt identity, or null when none is active.</summary>
        public async Task<PromptVersion> GetActiveAsync(long promptId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM PromptVersions WHERE PromptId = @PromptId AND IsActive = 1 LIMIT 1;";
            command.Parameters.AddWithValue("@PromptId", promptId);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the active system prompt version, or null when none is active.</summary>
        public async Task<PromptVersion> GetActiveSystemAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM PromptVersions
                WHERE IsActive = 1
                  AND PromptId = (SELECT Id FROM Prompts WHERE PromptType = @System LIMIT 1)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@System", (int)PromptType.System);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the active review-specific prompt version, or null when none is active.</summary>
        public async Task<PromptVersion> GetActiveByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM PromptVersions
                WHERE IsActive = 1
                  AND PromptId = (SELECT Id FROM Prompts WHERE PromptType = @Review AND ReviewTypeId = @ReviewTypeId LIMIT 1)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@Review", (int)PromptType.Review);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            return await ReadSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the highest version number currently assigned to a prompt identity, or 0.</summary>
        public async Task<int> GetMaxVersionNumberAsync(long promptId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(VersionNumber), 0) FROM PromptVersions WHERE PromptId = @PromptId;";
            command.Parameters.AddWithValue("@PromptId", promptId);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Inserts a prompt version and returns its assigned id.</summary>
        public async Task<long> AddAsync(PromptVersion version, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(version);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PromptVersions (
                    PromptId, VersionNumber, PromptText, Description, Origin, SourceOptimisationId, CreatedUtc, CreatedBy, IsActive, IsDraft, ActivatedUtc)
                VALUES (
                    @PromptId, @VersionNumber, @PromptText, @Description, @Origin, @SourceOptimisationId, @CreatedUtc, @CreatedBy, @IsActive, @IsDraft, @ActivatedUtc);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, version);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an editable draft. Returns false when the version is not an editable draft.</summary>
        public async Task<bool> UpdateDraftAsync(long versionId, string promptText, string description, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE PromptVersions
                SET PromptText = @PromptText,
                    Description = @Description
                WHERE Id = @Id AND IsDraft = 1 AND IsActive = 0;
                """;
            command.Parameters.AddWithValue("@Id", versionId);
            command.Parameters.AddWithValue("@PromptText", promptText ?? string.Empty);
            command.Parameters.AddWithValue("@Description", description ?? string.Empty);

            int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected > 0;
        }

        /// <summary>Commits a draft as an immutable historical version. Returns null when the version is not a draft.</summary>
        public async Task<PromptVersion> CommitDraftAsync(long versionId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                UPDATE PromptVersions
                SET IsDraft = 0
                WHERE Id = @Id AND IsDraft = 1 AND IsActive = 0
                RETURNING {SelectColumns};
                """;
            command.Parameters.AddWithValue("@Id", versionId);

            return await ReadReturningSingleAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Activates a version and deactivates every other version of the same prompt identity.</summary>
        public async Task<PromptVersion> ActivateAsync(long versionId, string activatedBy, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            using (SqliteCommand deactivate = connection.CreateCommand())
            {
                deactivate.Transaction = transaction;
                deactivate.CommandText = """
                    UPDATE PromptVersions
                    SET IsActive = 0
                    WHERE PromptId = (SELECT PromptId FROM PromptVersions WHERE Id = @Id)
                      AND IsActive = 1;
                    """;
                deactivate.Parameters.AddWithValue("@Id", versionId);
                _ = await deactivate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using SqliteCommand activate = connection.CreateCommand();
            activate.Transaction = transaction;
            activate.CommandText = $"""
                UPDATE PromptVersions
                SET IsActive = 1,
                    IsDraft = 0,
                    ActivatedUtc = @ActivatedUtc
                WHERE Id = @Id
                RETURNING {SelectColumns};
                """;
            activate.Parameters.AddWithValue("@Id", versionId);
            activate.Parameters.AddWithValue("@ActivatedUtc", SqliteValueConverter.ToText(utcNow));

            PromptVersion activated = await ReadReturningSingleAsync(activate, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return activated;
        }

        private static void AddParameters(SqliteCommand command, PromptVersion version)
        {
            command.Parameters.AddWithValue("@PromptId", version.PromptId);
            command.Parameters.AddWithValue("@VersionNumber", version.VersionNumber);
            command.Parameters.AddWithValue("@PromptText", version.PromptText ?? string.Empty);
            command.Parameters.AddWithValue("@Description", version.Description ?? string.Empty);
            command.Parameters.AddWithValue("@Origin", (int)version.Origin);
            command.Parameters.AddWithValue("@SourceOptimisationId", SqliteValueConverter.ToParameter(version.SourceOptimisationId));
            command.Parameters.AddWithValue("@CreatedUtc", SqliteValueConverter.ToText(version.CreatedUtc));
            command.Parameters.AddWithValue("@CreatedBy", version.CreatedBy ?? string.Empty);
            command.Parameters.AddWithValue("@IsActive", version.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@IsDraft", version.IsDraft ? 1 : 0);
            command.Parameters.AddWithValue("@ActivatedUtc", SqliteValueConverter.ToParameter(version.ActivatedUtc));
        }

        private static async Task<PromptVersion> ReadSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
        {
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        private static async Task<PromptVersion> ReadReturningSingleAsync(SqliteCommand command, CancellationToken cancellationToken)
        {
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Map(reader);
            }

            return null;
        }

        private static PromptVersion Map(SqliteDataReader reader)
        {
            return new PromptVersion
            {
                Id = reader.GetInt64(0),
                PromptId = reader.GetInt64(1),
                VersionNumber = reader.GetInt32(2),
                PromptText = reader.GetString(3),
                Description = reader.GetString(4),
                Origin = (PromptOrigin)reader.GetInt32(5),
                SourceOptimisationId = reader.GetNullableInt64(6),
                CreatedUtc = SqliteValueConverter.ToDateTime(reader.GetString(7)),
                CreatedBy = reader.GetString(8),
                IsActive = reader.GetInt32(9) == 1,
                IsDraft = reader.GetInt32(10) == 1,
                ActivatedUtc = reader.IsDBNull(11) ? null : SqliteValueConverter.ToDateTime(reader.GetString(11))
            };
        }
    }
}
