using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IReviewLinkRepository"/>.</summary>
    public sealed class ReviewLinkRepository : SqliteRepositoryBase, IReviewLinkRepository
    {
        private const string SelectColumns = "Id, RepoFileId, ReviewTypeId, Enabled, PriorityOverride";

        /// <summary>Initialises a new repository.</summary>
        public ReviewLinkRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the link with the given id, or null when not found.</summary>
        public async Task<ReviewLink> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewLinks WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapReviewLink(reader);
            }

            return null;
        }

        /// <summary>Returns the link between a file and a review type, or null when not found.</summary>
        public async Task<ReviewLink> GetAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewLinks WHERE RepoFileId = @RepoFileId AND ReviewTypeId = @ReviewTypeId LIMIT 1;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapReviewLink(reader);
            }

            return null;
        }

        /// <summary>Returns all links for a file.</summary>
        public async Task<IReadOnlyList<ReviewLink>> GetByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewLinks WHERE RepoFileId = @RepoFileId;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            List<ReviewLink> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewLink(reader));
            }

            return results;
        }

        /// <summary>Returns all links for a review type.</summary>
        public async Task<IReadOnlyList<ReviewLink>> GetByReviewTypeAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewLinks WHERE ReviewTypeId = @ReviewTypeId;";
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);

            List<ReviewLink> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewLink(reader));
            }

            return results;
        }

        /// <summary>Inserts or updates the link and returns its id.</summary>
        public async Task<long> UpsertAsync(ReviewLink link, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ReviewLinks (RepoFileId, ReviewTypeId, Enabled, PriorityOverride)
                VALUES (@RepoFileId, @ReviewTypeId, @Enabled, @PriorityOverride)
                ON CONFLICT (RepoFileId, ReviewTypeId) DO UPDATE SET
                    Enabled = excluded.Enabled,
                    PriorityOverride = excluded.PriorityOverride;
                SELECT Id FROM ReviewLinks WHERE RepoFileId = @RepoFileId AND ReviewTypeId = @ReviewTypeId;
                """;
            AddParameters(command, link);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Deletes the link with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ReviewLinks WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes all links for a file.</summary>
        public async Task DeleteByRepoFileAsync(long repoFileId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ReviewLinks WHERE RepoFileId = @RepoFileId;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, ReviewLink link)
        {
            command.Parameters.AddWithValue("@RepoFileId", link.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", link.ReviewTypeId);
            command.Parameters.AddWithValue("@Enabled", link.Enabled);
            command.Parameters.AddWithValue("@PriorityOverride", SqliteValueConverter.ToParameter(link.PriorityOverride));
        }

        private static ReviewLink MapReviewLink(SqliteDataReader reader)
        {
            return new ReviewLink
            {
                Id = reader.GetInt64(0),
                RepoFileId = reader.GetInt64(1),
                ReviewTypeId = reader.GetInt64(2),
                Enabled = reader.GetBoolean(3),
                PriorityOverride = reader.IsDBNull(4) ? null : (int)reader.GetInt64(4)
            };
        }
    }
}
