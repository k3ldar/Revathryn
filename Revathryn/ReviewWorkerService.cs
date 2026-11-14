using Revathryn.Contracts;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Reliability;
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
        private readonly IProcessingGate _processingGate;
        private readonly IProcessingPauseRevalidator _pauseRevalidator;
        private readonly ILogger<ReviewWorkerService> _logger;

        /// <summary>Initialises a new review worker.</summary>
        public ReviewWorkerService(
            IRepositoryProcessor repositoryProcessor,
            IReviewProcessor reviewProcessor,
            IProcessingGate processingGate,
            IProcessingPauseRevalidator pauseRevalidator,
            ILogger<ReviewWorkerService> logger)
        {
            _repositoryProcessor = repositoryProcessor;
            _reviewProcessor = reviewProcessor;
            _processingGate = processingGate;
            _pauseRevalidator = pauseRevalidator;
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
            if (_processingGate.IsPaused)
            {
                if (!await TryResumeProcessingAsync(cancellationToken))
                {
                    return;
                }
            }

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

        private async Task<bool> TryResumeProcessingAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (await _pauseRevalidator.TryResumeAsync(cancellationToken))
                {
                    _logger.LogInformation(Strings.ReviewProcessingResumed);
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, Strings.ReviewWorkerCycleFailed);
            }

            return false;
        }

        private async Task DrainQueueAsync(CancellationToken cancellationToken)
        {
            await QueueDrainHelper.DrainAsync(
                _reviewProcessor.ProcessNextAsync,
                Consts.ReviewWorkerMaxItemsPerCycle,
                cancellationToken,
                () => !_processingGate.IsPaused).ConfigureAwait(false);
        }
    }
}
