using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn
{
    /// <summary>
    /// Periodically scans enabled repositories to schedule outstanding review
    /// work and then drains the review queue until it is empty.
    /// </summary>
    public sealed class ReviewWorkerService : BackgroundService
    {
        private readonly IRepositoryProcessor _repositoryProcessor;
        private readonly IReviewProcessor _reviewProcessor;
        private readonly ILogger<ReviewWorkerService> _logger;

        /// <summary>Initialises a new review worker.</summary>
        public ReviewWorkerService(
            IRepositoryProcessor repositoryProcessor,
            IReviewProcessor reviewProcessor,
            ILogger<ReviewWorkerService> logger)
        {
            _repositoryProcessor = repositoryProcessor;
            _reviewProcessor = reviewProcessor;
            _logger = logger;
        }

        /// <summary>Runs the periodic scan and drain loop until the host stops.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);

                try
                {
                    await Task.Delay(Consts.ReviewWorkerDelayMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunCycleAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _repositoryProcessor.ProcessAllEnabledAsync(cancellationToken);
                await DrainQueueAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, Strings.ReviewWorkerCycleFailed);
            }
        }

        private async Task DrainQueueAsync(CancellationToken cancellationToken)
        {
            int processed = 0;

            while (processed < Consts.ReviewWorkerMaxItemsPerCycle)
            {
                bool handled = await _reviewProcessor.ProcessNextAsync(cancellationToken);

                if (!handled)
                {
                    return;
                }

                processed++;
            }
        }
    }
}
