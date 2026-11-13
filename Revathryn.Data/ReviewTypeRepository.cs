using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IReviewTypeRepository"/>.</summary>
    public sealed class ReviewTypeRepository : SqliteRepositoryBase, IReviewTypeRepository
    {
        private const string SelectColumns =
            "rt.Id, rt.Key, rt.Name, rt.Description, rt.FileExtensions, rt.Prompt, rt.DefaultPriority, rt.ReviewDefinitionVersion, rt.CreatedDate, rt.LastModifiedDate";

        private const string SelectRepositoryColumns = SelectColumns + ", rr.Enabled";

        /// <summary>Initialises a new repository.</summary>
        public ReviewTypeRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the review type with the given id, or null when not found.</summary>
        public async Task<ReviewType> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewTypes rt WHERE rt.Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapReviewType(reader);
            }

            return null;
        }

        /// <summary>Returns every global review type.</summary>
        public async Task<IReadOnlyList<ReviewType>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewTypes rt ORDER BY rt.Name ASC;";

            List<ReviewType> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewType(reader));
            }

            return results;
        }

        /// <summary>Returns the review types linked to a repository.</summary>
        public async Task<IReadOnlyList<ReviewType>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM ReviewTypes rt
                INNER JOIN RepositoryReviews rr ON rr.ReviewTypeId = rt.Id
                WHERE rr.RepositoryId = @RepositoryId
                ORDER BY rt.Name ASC;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<ReviewType> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewType(reader));
            }

            return results;
        }

        /// <summary>Returns the enabled review types linked to a repository.</summary>
        public async Task<IReadOnlyList<ReviewType>> GetEnabledByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns}
                FROM ReviewTypes rt
                INNER JOIN RepositoryReviews rr ON rr.ReviewTypeId = rt.Id
                WHERE rr.RepositoryId = @RepositoryId AND rr.Enabled = 1
                ORDER BY rt.Name ASC;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<ReviewType> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewType(reader));
            }

            return results;
        }

        /// <summary>Returns the repository-linked review types including their enabled state.</summary>
        public async Task<IReadOnlyList<RepositoryReviewType>> GetRepositoryReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectRepositoryColumns}
                FROM ReviewTypes rt
                INNER JOIN RepositoryReviews rr ON rr.ReviewTypeId = rt.Id
                WHERE rr.RepositoryId = @RepositoryId
                ORDER BY rt.Name ASC;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<RepositoryReviewType> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRepositoryReviewType(reader));
            }

            return results;
        }

        /// <summary>Inserts the review type and returns its assigned id.</summary>
        public async Task<long> AddAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ReviewTypes (Key, Name, Description, FileExtensions, Prompt, DefaultPriority, ReviewDefinitionVersion, CreatedDate, LastModifiedDate)
                VALUES (@Key, @Name, @Description, @FileExtensions, @Prompt, @DefaultPriority, @ReviewDefinitionVersion, @CreatedDate, @LastModifiedDate);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, reviewType);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an existing review type.</summary>
        public async Task UpdateAsync(ReviewType reviewType, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE ReviewTypes
                SET Key = @Key,
                    Name = @Name,
                    Description = @Description,
                    FileExtensions = @FileExtensions,
                    Prompt = @Prompt,
                    DefaultPriority = @DefaultPriority,
                    ReviewDefinitionVersion = @ReviewDefinitionVersion,
                    CreatedDate = @CreatedDate,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
                """;
            AddParameters(command, reviewType);
            command.Parameters.AddWithValue("@Id", reviewType.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the review type with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ReviewTypes WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, ReviewType reviewType)
        {
            command.Parameters.AddWithValue("@Key", reviewType.Key);
            command.Parameters.AddWithValue("@Name", reviewType.Name);
            command.Parameters.AddWithValue("@Description", reviewType.Description);
            command.Parameters.AddWithValue("@FileExtensions", reviewType.FileExtensions);
            command.Parameters.AddWithValue("@Prompt", reviewType.Prompt);
            command.Parameters.AddWithValue("@DefaultPriority", reviewType.DefaultPriority);
            command.Parameters.AddWithValue("@ReviewDefinitionVersion", reviewType.ReviewDefinitionVersion);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(reviewType.CreatedDate));
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(reviewType.LastModifiedDate));
        }

        private static ReviewType MapReviewType(SqliteDataReader reader)
        {
            return new ReviewType
            {
                Id = reader.GetInt64(0),
                Key = reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.GetString(3),
                FileExtensions = reader.GetString(4),
                Prompt = reader.GetString(5),
                DefaultPriority = reader.GetInt32(6),
                ReviewDefinitionVersion = reader.GetString(7),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(8)),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(9))
            };
        }

        private static RepositoryReviewType MapRepositoryReviewType(SqliteDataReader reader)
        {
            return new RepositoryReviewType
            {
                ReviewType = MapReviewType(reader),
                Enabled = reader.GetBoolean(10)
            };
        }
    }
}

