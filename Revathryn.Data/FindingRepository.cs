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
            "Id, RepositoryId, RepoFileId, ReviewTypeId, ReviewContextId, Fingerprint, Severity, Title, Description, SuggestedFix, Status, Category, CreatedDate, LastModifiedDate, DuplicateOfFindingId, DuplicateSimilarityScore, FindingKey, LineStart, LineEnd, ResolutionOrigin, ResolvedUtc, ResolvedReviewSha256, EngineModel, Branch";

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

        /// <summary>Returns a filtered, sorted and paged set of findings.</summary>
        public async Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            string whereClause = BuildWhereClause(query, out List<(string Name, object Value)> parameters);

            int totalCount;
            using (SqliteCommand countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = $"SELECT COUNT(*) FROM Findings{whereClause};";
                AddQueryParameters(countCommand, parameters);

                object result = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                totalCount = Convert.ToInt32(result);

                if (totalCount == 0)
                {
                    return new FindingQueryResult
                    {
                        Items = [],
                        TotalCount = 0
                    };
                }
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings{whereClause}{BuildOrderByClause(query)} LIMIT @PageSize OFFSET @Offset;";
            AddQueryParameters(command, parameters);
            command.Parameters.AddWithValue("@PageSize", query.PageSize);
            command.Parameters.AddWithValue("@Offset", query.PageIndex * query.PageSize);

            List<Finding> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapFinding(reader));
            }

            return new FindingQueryResult
            {
                Items = results,
                TotalCount = totalCount
            };
        }

        /// <summary>Returns the findings immediately before and after the given finding within the supplied query.</summary>
        public async Task<FindingNavigation> GetNavigationAsync(long findingId, FindingQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            string whereClause = BuildWhereClause(query, out List<(string Name, object Value)> parameters);
            string orderByColumns = BuildOrderByColumns(query);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $@"
                WITH ranked AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY {orderByColumns}) AS RowNum
                    FROM Findings
                    {whereClause}
                )
                SELECT
                    (SELECT Id FROM ranked WHERE RowNum = (SELECT RowNum FROM ranked WHERE Id = @FindingId) - 1),
                    (SELECT Id FROM ranked WHERE RowNum = (SELECT RowNum FROM ranked WHERE Id = @FindingId) + 1);";
            AddQueryParameters(command, parameters);
            command.Parameters.AddWithValue("@FindingId", findingId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new FindingNavigation
                {
                    PreviousId = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0),
                    NextId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1)
                };
            }

            return new FindingNavigation();
        }

        /// <summary>Returns total issue counts grouped by repository.</summary>
        public async Task<IReadOnlyList<RepositoryIssueCount>> CountByRepositoryAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT RepositoryId, COUNT(*) FROM Findings GROUP BY RepositoryId;";

            List<RepositoryIssueCount> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new RepositoryIssueCount
                {
                    RepositoryId = reader.GetInt64(0),
                    Count = reader.GetInt32(1)
                });
            }

            return results;
        }

        /// <summary>Returns all outstanding findings, most recent first.</summary>
        public async Task<IReadOnlyList<Finding>> GetOutstandingAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE Status IN (@StatusNew, @StatusReviewed, @StatusDeferred) ORDER BY CreatedDate DESC;";
            command.Parameters.AddWithValue("@StatusNew", (int)FindingStatus.New);
            command.Parameters.AddWithValue("@StatusReviewed", (int)FindingStatus.Reviewed);
            command.Parameters.AddWithValue("@StatusDeferred", (int)FindingStatus.Deferred);

            List<Finding> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapFinding(reader));
            }

            return results;
        }

        /// <summary>Returns the number of outstanding operational failure findings.</summary>
        public async Task<int> CountOutstandingOperationalFailuresAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Findings WHERE Category = @Category AND Status IN (@StatusNew, @StatusReviewed, @StatusDeferred);";
            command.Parameters.AddWithValue("@Category", (int)FindingCategory.OperationalFailure);
            command.Parameters.AddWithValue("@StatusNew", (int)FindingStatus.New);
            command.Parameters.AddWithValue("@StatusReviewed", (int)FindingStatus.Reviewed);
            command.Parameters.AddWithValue("@StatusDeferred", (int)FindingStatus.Deferred);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        /// <summary>Returns the code-issue findings in a repository that are eligible for similarity matching.</summary>
        public async Task<IReadOnlyList<Finding>> GetSimilarityCandidatesAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE RepositoryId = @RepositoryId AND Category = @Category ORDER BY Id ASC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@Category", (int)FindingCategory.CodeIssue);

            List<Finding> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapFinding(reader));
            }

            return results;
        }

        /// <summary>Records that a finding duplicates another finding with the given similarity score.</summary>
        public async Task MarkDuplicateAsync(long findingId, long duplicateOfFindingId, double similarityScore, DateTime lastModifiedUtc, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Findings
                SET DuplicateOfFindingId = @DuplicateOfFindingId,
                    DuplicateSimilarityScore = @DuplicateSimilarityScore,
                    LastModifiedDate = @LastModifiedDate
                WHERE Id = @Id;
                """;
            command.Parameters.AddWithValue("@Id", findingId);
            command.Parameters.AddWithValue("@DuplicateOfFindingId", duplicateOfFindingId);
            command.Parameters.AddWithValue("@DuplicateSimilarityScore", similarityScore);
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(lastModifiedUtc));

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts the finding, or returns the existing id when an equivalent finding already exists.</summary>
        public async Task<long> AddAsync(Finding finding, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await AddAsync(connection, null, finding, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts the finding within the supplied transaction, or returns the existing id when already present.</summary>
        internal static async Task<long> AddAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            Finding finding,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Findings (RepositoryId, RepoFileId, ReviewTypeId, ReviewContextId, Fingerprint, Severity, Title, Description, SuggestedFix, Status, Category, CreatedDate, LastModifiedDate, DuplicateOfFindingId, DuplicateSimilarityScore, FindingKey, LineStart, LineEnd, ResolutionOrigin, ResolvedUtc, ResolvedReviewSha256, EngineModel, Branch)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @ReviewContextId, @Fingerprint, @Severity, @Title, @Description, @SuggestedFix, @Status, @Category, @CreatedDate, @LastModifiedDate, @DuplicateOfFindingId, @DuplicateSimilarityScore, @FindingKey, @LineStart, @LineEnd, @ResolutionOrigin, @ResolvedUtc, @ResolvedReviewSha256, @EngineModel, @Branch);
                SELECT Id FROM Findings WHERE RepositoryId = @RepositoryId AND Fingerprint = @Fingerprint LIMIT 1;
                """;
            AddParameters(command, finding);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            long id = (long)result;
            finding.Id = id;
            return id;
        }

        /// <summary>Returns all findings recorded for the given file, review type, and branch within the supplied transaction.</summary>
        internal static async Task<IReadOnlyList<Finding>> GetForFileAndReviewTypeAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long repoFileId,
            long reviewTypeId,
            string branch,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {SelectColumns} FROM Findings WHERE RepoFileId = @RepoFileId AND ReviewTypeId = @ReviewTypeId AND Branch = @Branch ORDER BY Id ASC;";
            command.Parameters.AddWithValue("@RepoFileId", repoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", reviewTypeId);
            command.Parameters.AddWithValue("@Branch", branch ?? string.Empty);

            List<Finding> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapFinding(reader));
            }

            return results;
        }

        /// <summary>Updates an existing finding.</summary>
        public async Task UpdateAsync(Finding finding, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await UpdateAsync(connection, null, finding, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Updates the finding within the supplied transaction.</summary>
        internal static async Task UpdateAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            Finding finding,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Findings
                SET RepositoryId = @RepositoryId,
                    RepoFileId = @RepoFileId,
                    ReviewTypeId = @ReviewTypeId,
                    ReviewContextId = @ReviewContextId,
                    Fingerprint = @Fingerprint,
                    Severity = @Severity,
                    Title = @Title,
                    Description = @Description,
                    SuggestedFix = @SuggestedFix,
                    Status = @Status,
                    Category = @Category,
                    CreatedDate = @CreatedDate,
                    LastModifiedDate = @LastModifiedDate,
                    DuplicateOfFindingId = @DuplicateOfFindingId,
                    DuplicateSimilarityScore = @DuplicateSimilarityScore,
                    FindingKey = @FindingKey,
                    LineStart = @LineStart,
                    LineEnd = @LineEnd,
                    ResolutionOrigin = @ResolutionOrigin,
                    ResolvedUtc = @ResolvedUtc,
                    ResolvedReviewSha256 = @ResolvedReviewSha256,
                    EngineModel = @EngineModel,
                    Branch = @Branch
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

        private static string BuildWhereClause(FindingQuery query, out List<(string Name, object Value)> parameters)
        {
            parameters = [];
            List<string> conditions = [];

            if (query.RepositoryId.HasValue)
            {
                conditions.Add("RepositoryId = @RepositoryId");
                parameters.Add(("@RepositoryId", query.RepositoryId.Value));
            }

            if (query.Severity.HasValue)
            {
                conditions.Add("Severity = @Severity");
                parameters.Add(("@Severity", (int)query.Severity.Value));
            }

            if (query.ReviewTypeId.HasValue)
            {
                conditions.Add("ReviewTypeId = @ReviewTypeId");
                parameters.Add(("@ReviewTypeId", query.ReviewTypeId.Value));
            }

            if (query.Category.HasValue)
            {
                conditions.Add("Category = @Category");
                parameters.Add(("@Category", (int)query.Category.Value));
            }

            if (query.Status.HasValue)
            {
                conditions.Add("Status = @Status");
                parameters.Add(("@Status", (int)query.Status.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                conditions.Add("(Title LIKE @SearchText OR Description LIKE @SearchText)");
                parameters.Add(("@SearchText", $"%{query.SearchText.Trim()}%"));
            }

            if (conditions.Count == 0)
            {
                return string.Empty;
            }

            return $" WHERE {string.Join(" AND ", conditions)}";
        }

        private static string BuildOrderByClause(FindingQuery query)
        {
            return $" ORDER BY {BuildOrderByColumns(query)}";
        }

        private static string BuildOrderByColumns(FindingQuery query)
        {
            string column = query.SortField switch
            {
                FindingSortField.LastModifiedDate => "LastModifiedDate",
                FindingSortField.Severity => "Severity",
                FindingSortField.Title => "Title",
                _ => "CreatedDate"
            };

            string direction = query.SortDescending ? "DESC" : "ASC";
            string orderBy = $"{column} {direction}";

            if (query.SortField == FindingSortField.Severity)
            {
                orderBy += ", CreatedDate DESC";
            }

            return $"{orderBy}, Id ASC";
        }

        private static void AddQueryParameters(SqliteCommand command, List<(string Name, object Value)> parameters)
        {
            foreach ((string name, object value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        private static void AddParameters(SqliteCommand command, Finding finding)
        {
            command.Parameters.AddWithValue("@RepositoryId", finding.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", SqliteValueConverter.ToParameter(finding.RepoFileId));
            command.Parameters.AddWithValue("@ReviewTypeId", SqliteValueConverter.ToParameter(finding.ReviewTypeId));
            command.Parameters.AddWithValue("@ReviewContextId", SqliteValueConverter.ToParameter(finding.ReviewContextId));
            command.Parameters.AddWithValue("@Fingerprint", finding.Fingerprint);
            command.Parameters.AddWithValue("@Severity", (int)finding.Severity);
            command.Parameters.AddWithValue("@Title", finding.Title);
            command.Parameters.AddWithValue("@Description", finding.Description);
            command.Parameters.AddWithValue("@SuggestedFix", finding.SuggestedFix);
            command.Parameters.AddWithValue("@Status", (int)finding.Status);
            command.Parameters.AddWithValue("@Category", (int)finding.Category);
            command.Parameters.AddWithValue("@CreatedDate", SqliteValueConverter.ToText(finding.CreatedDate));
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(finding.LastModifiedDate));
            command.Parameters.AddWithValue("@DuplicateOfFindingId", SqliteValueConverter.ToParameter(finding.DuplicateOfFindingId));
            command.Parameters.AddWithValue("@DuplicateSimilarityScore", SqliteValueConverter.ToParameter(finding.DuplicateSimilarityScore));
            command.Parameters.AddWithValue("@FindingKey", finding.FindingKey ?? string.Empty);
            command.Parameters.AddWithValue("@LineStart", SqliteValueConverter.ToParameter(finding.LineStart));
            command.Parameters.AddWithValue("@LineEnd", SqliteValueConverter.ToParameter(finding.LineEnd));
            command.Parameters.AddWithValue("@ResolutionOrigin", (int)finding.ResolutionOrigin);
            command.Parameters.AddWithValue("@ResolvedUtc", SqliteValueConverter.ToParameter(finding.ResolvedUtc));
            command.Parameters.AddWithValue("@ResolvedReviewSha256", finding.ResolvedReviewSha256 ?? string.Empty);
            command.Parameters.AddWithValue("@EngineModel", finding.EngineModel ?? string.Empty);
            command.Parameters.AddWithValue("@Branch", finding.Branch ?? string.Empty);
        }

        private static Finding MapFinding(SqliteDataReader reader)
        {
            return new Finding
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                ReviewTypeId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                ReviewContextId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                Fingerprint = reader.GetString(5),
                Severity = (FindingSeverity)reader.GetInt32(6),
                Title = reader.GetString(7),
                Description = reader.GetString(8),
                SuggestedFix = reader.GetString(9),
                Status = (FindingStatus)reader.GetInt32(10),
                Category = (FindingCategory)reader.GetInt32(11),
                CreatedDate = SqliteValueConverter.ToDateTime(reader.GetString(12)),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(13)),
                DuplicateOfFindingId = reader.IsDBNull(14) ? null : reader.GetInt64(14),
                DuplicateSimilarityScore = reader.IsDBNull(15) ? null : reader.GetDouble(15),
                FindingKey = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                LineStart = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                LineEnd = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                ResolutionOrigin = (FindingResolutionOrigin)reader.GetInt32(19),
                ResolvedUtc = reader.IsDBNull(20) ? null : SqliteValueConverter.ToDateTime(reader.GetString(20)),
                ResolvedReviewSha256 = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                EngineModel = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                Branch = reader.IsDBNull(23) ? string.Empty : reader.GetString(23)
            };
        }
    }
}
