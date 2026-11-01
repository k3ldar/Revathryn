using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IReviewUsageRepository"/>.</summary>
    public sealed class ReviewUsageRepository : SqliteRepositoryBase, IReviewUsageRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RepoFileId, ReviewTypeId, EngineId, EngineName, EngineModel, AttemptCount, PromptTokens, CompletionTokens, TotalTokens, CachedTokens, OccurredUtc";

        /// <summary>Initialises a new repository.</summary>
        public ReviewUsageRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Inserts a usage record and returns its assigned id.</summary>
        public async Task<long> AddAsync(ReviewUsage usage, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await AddAsync(connection, null, usage, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Inserts the usage record within the supplied transaction.</summary>
        internal static async Task<long> AddAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReviewUsage usage,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ReviewUsage (RepositoryId, RepoFileId, ReviewTypeId, EngineId, EngineName, EngineModel, AttemptCount, PromptTokens, CompletionTokens, TotalTokens, CachedTokens, OccurredUtc)
                VALUES (@RepositoryId, @RepoFileId, @ReviewTypeId, @EngineId, @EngineName, @EngineModel, @AttemptCount, @PromptTokens, @CompletionTokens, @TotalTokens, @CachedTokens, @OccurredUtc);
                SELECT last_insert_rowid();
                """;
            AddParameters(command, usage);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Returns all usage records, most recent first.</summary>
        public async Task<IReadOnlyList<ReviewUsage>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ReviewUsage ORDER BY OccurredUtc DESC, Id DESC;";

            List<ReviewUsage> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapReviewUsage(reader));
            }

            return results;
        }

        /// <summary>Returns aggregated usage metrics for the dashboard.</summary>
        public async Task<ReviewUsageSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    COUNT(*),
                    COALESCE(SUM(PromptTokens), 0),
                    COALESCE(SUM(CompletionTokens), 0),
                    COALESCE(SUM(TotalTokens), 0),
                    COALESCE(SUM(CachedTokens), 0),
                    COALESCE(SUM(CASE WHEN OccurredUtc >= @TodayStart THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN OccurredUtc >= @TodayStart THEN PromptTokens ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN OccurredUtc >= @TodayStart THEN CompletionTokens ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN OccurredUtc >= @TodayStart THEN TotalTokens ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN OccurredUtc >= @TodayStart THEN CachedTokens ELSE 0 END), 0)
                FROM ReviewUsage;
                """;
            command.Parameters.AddWithValue("@TodayStart", SqliteValueConverter.ToText(DateTime.UtcNow.Date));

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ReviewUsageSummary();
            }

            int totalPromptTokens = Convert.ToInt32(reader.GetValue(1));
            int totalCachedTokens = Convert.ToInt32(reader.GetValue(4));
            int todayPromptTokens = Convert.ToInt32(reader.GetValue(6));
            int todayCachedTokens = Convert.ToInt32(reader.GetValue(9));

            return new ReviewUsageSummary
            {
                TotalReviewCount = Convert.ToInt32(reader.GetValue(0)),
                TotalPromptTokens = totalPromptTokens,
                TotalCompletionTokens = Convert.ToInt32(reader.GetValue(2)),
                TotalTokens = Convert.ToInt32(reader.GetValue(3)),
                TotalCachedTokens = totalCachedTokens,
                TotalCacheHitPercent = CalculateCacheHitPercent(totalPromptTokens, totalCachedTokens),
                TodayReviewCount = Convert.ToInt32(reader.GetValue(5)),
                TodayPromptTokens = todayPromptTokens,
                TodayCompletionTokens = Convert.ToInt32(reader.GetValue(7)),
                TodayTotalTokens = Convert.ToInt32(reader.GetValue(8)),
                TodayCachedTokens = todayCachedTokens,
                TodayCacheHitPercent = CalculateCacheHitPercent(todayPromptTokens, todayCachedTokens)
            };
        }

        private static void AddParameters(SqliteCommand command, ReviewUsage usage)
        {
            command.Parameters.AddWithValue("@RepositoryId", usage.RepositoryId);
            command.Parameters.AddWithValue("@RepoFileId", usage.RepoFileId);
            command.Parameters.AddWithValue("@ReviewTypeId", usage.ReviewTypeId);
            command.Parameters.AddWithValue("@EngineId", usage.EngineId);
            command.Parameters.AddWithValue("@EngineName", usage.EngineName);
            command.Parameters.AddWithValue("@EngineModel", usage.EngineModel);
            command.Parameters.AddWithValue("@AttemptCount", usage.AttemptCount);
            command.Parameters.AddWithValue("@PromptTokens", usage.PromptTokens);
            command.Parameters.AddWithValue("@CompletionTokens", usage.CompletionTokens);
            command.Parameters.AddWithValue("@TotalTokens", usage.TotalTokens);
            command.Parameters.AddWithValue("@CachedTokens", usage.CachedTokens);
            command.Parameters.AddWithValue("@OccurredUtc", SqliteValueConverter.ToText(usage.OccurredUtc));
        }

        private static ReviewUsage MapReviewUsage(SqliteDataReader reader)
        {
            return new ReviewUsage
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RepoFileId = reader.GetInt64(2),
                ReviewTypeId = reader.GetInt64(3),
                EngineId = reader.GetInt64(4),
                EngineName = reader.GetString(5),
                EngineModel = reader.GetString(6),
                AttemptCount = reader.GetInt32(7),
                PromptTokens = reader.GetInt32(8),
                CompletionTokens = reader.GetInt32(9),
                TotalTokens = reader.GetInt32(10),
                CachedTokens = reader.GetInt32(11),
                OccurredUtc = SqliteValueConverter.ToDateTime(reader.GetString(12))
            };
        }

        private static double CalculateCacheHitPercent(int promptTokens, int cachedTokens)
        {
            if (promptTokens <= 0)
            {
                return 0;
            }

            return (double)cachedTokens / promptTokens * 100;
        }
    }
}
