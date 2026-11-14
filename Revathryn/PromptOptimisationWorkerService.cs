using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Resources;

namespace Revathryn
{
    /// <summary>
    /// Polls for queued prompt optimisations and executes them in the background so that no HTTP request
    /// is held open while the review engine is running.
    /// </summary>
    public sealed class PromptOptimisationWorkerService : BackgroundService
    {
        private readonly IPromptOptimisationProcessor _processor;
        private readonly ILogger<PromptOptimisationWorkerService> _logger;

        /// <summary>Initialises a new prompt optimisation worker.</summary>
        public PromptOptimisationWorkerService(IPromptOptimisationProcessor processor, ILogger<PromptOptimisationWorkerService> logger)
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
                    await Task.Delay(TimeSpan.FromSeconds(Consts.PromptOptimisationPollIntervalSeconds), stoppingToken);
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
                await QueueDrainHelper.DrainAsync(_processor.ProcessNextAsync, Consts.PromptOptimisationDrainLimit, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, Strings.PromptOptimisationWorkerCycleFailed);
            }
        }
    }
}
