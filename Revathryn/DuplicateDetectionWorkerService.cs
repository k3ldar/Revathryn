using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Resources;

namespace Revathryn
{
    /// <summary>
    /// Polls for queued duplicate-detection items and executes them in the background so no HTTP
    /// request is held open while findings are compared.
    /// </summary>
    public sealed class DuplicateDetectionWorkerService : BackgroundService
    {
        private readonly IDuplicateDetectionProcessor _processor;
        private readonly ILogger<DuplicateDetectionWorkerService> _logger;

        /// <summary>Initialises a new duplicate-detection worker.</summary>
        public DuplicateDetectionWorkerService(IDuplicateDetectionProcessor processor, ILogger<DuplicateDetectionWorkerService> logger)
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
                    await Task.Delay(TimeSpan.FromSeconds(Consts.DuplicateDetectionPollIntervalSeconds), stoppingToken);
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

                while (processed < Consts.DuplicateDetectionDrainLimit && await _processor.ProcessNextAsync(cancellationToken))
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
                _logger.LogError(exception, Strings.DuplicateDetectionWorkerCycleFailed);
            }
        }
    }
}
