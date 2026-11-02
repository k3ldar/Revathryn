using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Manages the independent review engine configuration.</summary>
    public interface IReviewEngineConfigurationService
    {
        /// <summary>Returns the saved settings, or defaults when none have been saved.</summary>
        Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns true when settings have been saved.</summary>
        Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

        /// <summary>Validates settings without saving them.</summary>
        ReviewEngineValidationResult Validate(ReviewEngineSettings settings);

        /// <summary>Validates and saves settings. Nothing is saved when validation fails.</summary>
        Task<ReviewEngineValidationResult> SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default);

        /// <summary>Reports whether validation can currently be requested.</summary>
        Task<ReviewEngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

        /// <summary>Tests connectivity using the supplied settings. The result never contains credentials.</summary>
        Task<ReviewEngineConnectionResult> TestConnectionAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default);
    }
}
