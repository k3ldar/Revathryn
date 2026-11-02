using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Resources;

namespace Revathryn
{
    /// <summary>
    /// Polls for queued issue validations and executes them in the background so that no HTTP request
    /// is held open while the review engine is running.
    /// </summary>
    public sealed class IssueValidationWorkerService : BackgroundService
    {
        private readonly IIssueValidationProcessor _processor;
        private readonly ILogger<IssueValidationWorkerService> _logger;

        /// <summary>Initialises a new validation worker.</summary>
        public IssueValidationWorkerService(IIssueValidationProcessor processor, ILogger<IssueValidationWorkerService> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        /// <summary>Runs the poll and drain loop until the host stops.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await DrainAsync(stoppingToken);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Consts.IssueValidationPollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task DrainAsync(CancellationToken cancellationToken)
        {
            try
            {
                int processed = 0;

                while (processed < Consts.IssueValidationDrainLimit && await _processor.ProcessNextAsync(cancellationToken))
                {
                    processed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, Strings.IssueValidationWorkerCycleFailed);
            }
        }
    }
}
