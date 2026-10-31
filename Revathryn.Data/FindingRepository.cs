using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IFindingRepository"/>.</summary>
    public sealed class FindingRepository : SqliteRepositoryBase, IFindingRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RepoFileId, ReviewTypeId, Fingerprint, Severity, Title, Description, SuggestedFix, Status, Category, CreatedDate, LastModifiedDate";

        /// <summary>Initialises a new repository.</summary>
        public FindingRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the finding with the given id, or null when not found.</summary>
        public async Task<Finding> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapFinding(reader);
            }

            return null;
        }

        /// <summary>Returns all findings belonging to a repository.</summary>
        public async Task<IReadOnlyList<Finding>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE RepositoryId = @RepositoryId ORDER BY CreatedDate DESC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<Finding> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapFinding(reader));
            }

            return results;
        }

        /// <summary>Returns the first finding matching the repository and fingerprint, or null.</summary>
        public async Task<Finding> GetByFingerprintAsync(long repositoryId, string fingerprint, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE RepositoryId = @RepositoryId AND Fingerprint = @Fingerprint LIMIT 1;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@Fingerprint", fingerprint);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapFinding(reader);
            }

            return null;
        }

        /// <summary>Inserts the finding, or returns the existing id when an equivalent finding already exists.</summary>
        public async Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO Findings (RepositoryId, RepoFileId, ReviewTypeId, Fingerprint, Severity, Title, Description, SuggestedFix, Status, Category, CreatedDate, LastModifiedDate)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @Fingerprint, @Severity, @Title, @Description, @SuggestedFix, @Status, @Category, @CreatedDate, @LastModifiedDate);
                SELECT Id FROM Findings WHERE RepositoryId = @RepositoryId AND Fingerprint = @Fingerprint LIMIT 1;
                """;
            AddParameters(command, finding);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Updates an existing finding.</summary>
        public async Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Findings
                SET RepositoryId = @RepositoryId,
                    RepoFileId = @RepoFileId,
                    ReviewTypeId = @ReviewTypeId,
                    Fingerprint = @Fingerprint,
                    Severity = @Severity,
                    Title = @Title,
                    Description = @Description,
                    SuggestedFix = @SuggestedFix,
                    Status = @Status,
                    Category = @Category,
                    CreatedDate = @CreatedDate,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
                """;
            AddParameters(command, finding);
            command.Parameters.AddWithValue("@Id", finding.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the finding with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Findings WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, Finding finding)
        {
            command.Parameters.AddWithValue("@RepositoryId", finding.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", SqliteValueConverter.ToParameter(finding.RepoFileId));
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(finding.ReviewTypeId));
            command.Parameters.AddWithValue("@Fingerprint", finding.Fingerprint);
            command.Parameters.AddWithValue("@Severity", (int)finding.Severity);
            command.Parameters.AddWithValue("@Title", finding.Title);
            command.Parameters.AddWithValue("@Description", finding.Description);
            command.Parameters.AddWithValue("@SuggestedFix", finding.SuggestedFix);
            command.Parameters.AddWithValue("@Status", (int)finding.Status);
            command.Parameters.AddWithValue("@Category", (int)finding.Category);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(finding.CreatedDate));
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(finding.LastModifiedDate));
        }

        private static Finding MapFinding(SqliteDataReader reader)
        {
            return new Finding
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                ReviewTypeId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                Fingerprint = reader.GetString(4),
                Severity = (FindingSeverity)reader.GetInt32(5),
                Title = reader.GetString(6),
                Description = reader.GetString(7),
                SuggestedFix = reader.GetString(8),
                Status = (FindingStatus)reader.GetInt32(9),
                Category = (FindingCategory)reader.GetInt32(10),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(11)),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(12))
            };
        }
    }
}
