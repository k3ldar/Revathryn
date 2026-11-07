using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine.Reliability
{
    /// <summary>
    /// Rechecks the review engine configuration and resumes automated processing when it is valid again.
    /// </summary>
    public sealed class ProcessingPauseRevalidator : IProcessingPauseRevalidator
    {
        private readonly IProcessingGate _processingGate;
        private readonly IReviewEngineConfigurationService _configurationService;

        /// <summary>Initialises a new pause revalidator.</summary>
        public ProcessingPauseRevalidator(
            IProcessingGate processingGate,
            IReviewEngineConfigurationService configurationService)
        {
            _processingGate = processingGate;
            _configurationService = configurationService;
        }

        /// <inheritdoc />
        public async Task<bool> TryResumeAsync(CancellationToken cancellationToken = default)
        {
            if (!_processingGate.IsPaused)
            {
                return false;
            }

            ReviewEngineAvailability availability = await _configurationService.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);

            if (availability.IsAvailable)
            {
                _processingGate.Resume();
                return true;
            }

            return false;
        }
    }
}
