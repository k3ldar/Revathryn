using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Resources;

namespace Revathryn
{
    /// <summary>
    /// Polls for queued isolated prompt test runs and executes them in the background so that no HTTP request
    /// is held open while the detection engine is running.
    /// </summary>
    public sealed class PromptTestRunWorkerService : BackgroundService
    {
        private readonly IPromptTestRunProcessor _processor;
        private readonly ILogger<PromptTestRunWorkerService> _logger;

        /// <summary>Initialises a new prompt test worker.</summary>
        public PromptTestRunWorkerService(IPromptTestRunProcessor processor, ILogger<PromptTestRunWorkerService> logger)
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
                    await Task.Delay(TimeSpan.FromSeconds(Consts.PromptTestRunPollIntervalSeconds), stoppingToken);
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

                while (processed < Consts.PromptTestRunDrainLimit && await _processor.ProcessNextAsync(cancellationToken))
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
                _logger.LogError(exception, Strings.PromptTestRunWorkerCycleFailed);
            }
        }
    }
}
