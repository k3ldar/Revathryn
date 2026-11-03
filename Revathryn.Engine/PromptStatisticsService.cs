using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Provides basic prompt effectiveness statistics. Agreement between AI models is an indicator, not ground
    /// truth, so these figures are presented as AI validation statistics unless a human confirmation exists.
    /// </summary>
    public sealed class PromptStatisticsService : IPromptStatisticsService
    {
        private const double MaxPercent = 100d;
        private readonly IPromptStatisticsRepository _statisticsRepository;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public PromptStatisticsService(
            IPromptStatisticsRepository statisticsRepository,
            IOperationalDiagnostics diagnostics)
        {
            _statisticsRepository = statisticsRepository;
            _diagnostics = diagnostics;
        }

        /// <summary>Returns basic statistics filtered by review type, prompt version, model and date range.</summary>
        public async Task<PromptStatistics> GetStatisticsAsync(PromptStatisticsFilter filter, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            PromptStatisticsSnapshot snapshot = await _statisticsRepository.GetSnapshotAsync(filter, cancellationToken).ConfigureAwait(false);

            PromptStatistics statistics = new()
            {
                IssuesDetected = snapshot.IssuesDetected,
                ValidatedIssues = snapshot.ValidatedIssues,
                AgreedCount = snapshot.AgreedCount,
                DisagreedCount = snapshot.DisagreedCount,
                PartiallyAgreedCount = snapshot.PartiallyAgreedCount,
                InconclusiveCount = snapshot.InconclusiveCount,
                AgreementRate = ComputeRate(snapshot.AgreedCount, snapshot.ValidatedIssues),
                DisagreementRate = ComputeRate(snapshot.DisagreedCount, snapshot.ValidatedIssues),
                PartialAgreementRate = ComputeRate(snapshot.PartiallyAgreedCount, snapshot.ValidatedIssues),
                InconclusiveRate = ComputeRate(snapshot.InconclusiveCount, snapshot.ValidatedIssues),
                PromptTests = snapshot.PromptTests,
                PromptActivations = snapshot.PromptActivations
            };

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptStatisticsServiceComponent,
                Consts.ComputeStatisticsOperation,
                Strings.PromptStatisticsComputedLog);

            return statistics;
        }

        private static double? ComputeRate(int count, int total)
        {
            if (total <= 0)
            {
                return null;
            }

            return (double)count / total * MaxPercent;
        }
    }
}
