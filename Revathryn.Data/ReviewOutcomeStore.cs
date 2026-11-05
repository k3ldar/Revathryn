using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;

namespace Revathryn.Data
{
    /// <summary>Persists a complete review outcome atomically using a single SQLite transaction.</summary>
    public sealed class ReviewOutcomeStore : IReviewOutcomeStore
    {
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly IFindingReconciler _findingReconciler;

        /// <summary>Initialises a new store.</summary>
        public ReviewOutcomeStore(SqliteConnectionFactory connectionFactory, IFindingReconciler findingReconciler)
        {
            _connectionFactory = connectionFactory;
            _findingReconciler = findingReconciler;
        }

        /// <summary>Persists findings and completed analysis in a single transaction.</summary>
        public async Task PersistAsync(ReviewOutcome outcome, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            SqliteConnection connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteTransactionScope scope = new(connection, transaction);

            long? reviewContextId = null;
            if (outcome.Context is not null)
            {
                reviewContextId = await ReviewContextRepository.AddAsync(connection, transaction, outcome.Context, cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<Finding> newFindings = outcome.Findings ?? [];

            foreach (Finding finding in newFindings)
            {
                finding.ReviewContextId ??= reviewContextId;
            }

            IReadOnlyList<Finding> existingFindings = await FindingRepository
                .GetForFileAndReviewTypeAsync(connection, transaction, outcome.RepoFileId, outcome.ReviewTypeId, cancellationToken)
                .ConfigureAwait(false);

            FindingReconciliation reconciliation = _findingReconciler.Reconcile(existingFindings, newFindings, outcome.Sha256, outcome.CompletedUtc);

            foreach (Finding finding in reconciliation.FindingsToInsert)
            {
                await FindingRepository.AddAsync(connection, transaction, finding, cancellationToken).ConfigureAwait(false);
            }

            foreach (Finding finding in reconciliation.FindingsToUpdate)
            {
                await FindingRepository.UpdateAsync(connection, transaction, finding, cancellationToken).ConfigureAwait(false);
            }

            foreach (Finding finding in reconciliation.FindingsToResolve)
            {
                await FindingRepository.UpdateAsync(connection, transaction, finding, cancellationToken).ConfigureAwait(false);
            }

            foreach (ReviewUsage usage in outcome.UsageRecords ?? [])
            {
                ReviewUsage record = new()
                {
                    RepositoryId = outcome.RepositoryId,
                    RepoFileId = outcome.RepoFileId,
                    ReviewTypeId = outcome.ReviewTypeId,
                    EngineId = usage.EngineId,
                    EngineName = usage.EngineName,
                    EngineModel = usage.EngineModel,
                    AttemptCount = usage.AttemptCount,
                    PromptTokens = usage.PromptTokens,
                    CompletionTokens = usage.CompletionTokens,
                    TotalTokens = usage.TotalTokens,
                    CachedTokens = usage.CachedTokens,
                    OccurredUtc = usage.OccurredUtc == default ? outcome.CompletedUtc : usage.OccurredUtc
                };

                await ReviewUsageRepository.AddAsync(connection, transaction, record, cancellationToken).ConfigureAwait(false);
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
