using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepositoryReviewRepository"/>.</summary>
    public sealed class RepositoryReviewRepository : SqliteRepositoryBase, IRepositoryReviewRepository
    {
        /// <summary>Initialises a new repository.</summary>
        public RepositoryReviewRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the link between a repository and review type, or null when not linked.</summary>
        public async Task<RepositoryReview> GetAsync(long repositoryId, long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, RepositoryId, ReviewTypeId, Enabled
                FROM RepositoryReviews
                WHERE RepositoryId = @RepositoryId AND ReviewTypeId = @ReviewTypeId
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapRepositoryReview(reader);
            }

            return null;
        }

        /// <summary>Returns all review type links for a repository.</summary>
        public async Task<IReadOnlyList<RepositoryReview>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, RepositoryId, ReviewTypeId, Enabled
                FROM RepositoryReviews
                WHERE RepositoryId = @RepositoryId
                ORDER BY Id ASC;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<RepositoryReview> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRepositoryReview(reader));
            }

            return results;
        }

        /// <summary>Links a review type to a repository and returns the assigned id.</summary>
        public async Task<long> AddAsync(RepositoryReview link, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO RepositoryReviews (RepositoryId, ReviewTypeId, Enabled)
                VALUES (@RepositoryId, @ReviewTypeId, @Enabled);
                SELECT Id FROM RepositoryReviews
                WHERE RepositoryId = @RepositoryId AND ReviewTypeId = @ReviewTypeId
                LIMIT 1;
                """;
            AddParameters(command, link);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Sets whether a review type is enabled for a repository, creating the link when absent.</summary>
        public async Task SetEnabledAsync(long repositoryId, long reviewTypeId, bool enabled, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO RepositoryReviews (RepositoryId, ReviewTypeId, Enabled)
                VALUES (@RepositoryId, @ReviewTypeId, @Enabled)
                ON CONFLICT (RepositoryId, ReviewTypeId)
                DO UPDATE SET Enabled = excluded.Enabled;
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);
            command.Parameters.AddWithValue("@Enabled", enabled);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Links every global review type to the repository; returns the number of new links.</summary>
        public async Task<int> LinkAllReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO RepositoryReviews (RepositoryId, ReviewTypeId, Enabled)
                SELECT @RepositoryId, Id, 1 FROM ReviewTypes;
                SELECT changes();
                """;
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        /// <summary>Links the review type to every repository; returns the number of new links.</summary>
        public async Task<int> LinkToAllRepositoriesAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO RepositoryReviews (RepositoryId, ReviewTypeId, Enabled)
                SELECT Id, @ReviewTypeId, 1 FROM Repositories;
                SELECT changes();
                """;
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        private static void AddParameters(SqliteCommand command, RepositoryReview link)
        {
            command.Parameters.AddWithValue("@RepositoryId", link.RepositoryId);
            command.Parameters.AddWithValue("@ReviewTypeId", link.ReviewTypeId);
            command.Parameters.AddWithValue("@Enabled", link.Enabled);
        }

        private static RepositoryReview MapRepositoryReview(SqliteDataReader reader)
        {
            return new RepositoryReview
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                ReviewTypeId = reader.GetInt64(2),
                Enabled = reader.GetBoolean(3)
            };
        }
    }
}
