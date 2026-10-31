using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>Persists a complete review outcome atomically using a single SQLite transaction.</summary>
    public sealed class ReviewOutcomeStore : IReviewOutcomeStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        /// <summary>Initialises a new store.</summary>
        public ReviewOutcomeStore(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>Persists findings and completed analysis in a single transaction.</summary>
        public async Task PersistAsync(ReviewOutcome outcome, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            SqliteConnection connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteTransactionScope scope = new(connection, transaction);

            foreach (Finding finding in outcome.Findings ?? [])
            {
                await FindingRepository.AddAsync(connection, transaction, finding, cancellationToken).ConfigureAwait(false);
            }

            RepoFileAnalysis analysis = new()
            {
                RepositoryId = outcome.RepositoryId,
                RepoFileId = outcome.RepoFileId,
                ReviewTypeId = outcome.ReviewTypeId,
                Sha256 = outcome.Sha256,
                LastAnalysedDate = outcome.CompletedUtc,
                AnalysisVersion = outcome.AnalysisVersion,
                ReviewDefinitionVersion = outcome.ReviewDefinitionVersion
            };

            await RepoFileAnalysisRepository.UpsertAsync(connection, transaction, analysis, cancellationToken).ConfigureAwait(false);

            scope.Commit();
        }
    }
}
