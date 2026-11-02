using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Tests
{
    /// <summary>Configurable in-memory review engine configuration service for controller tests.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class FakeReviewEngineConfigurationService : IReviewEngineConfigurationService
    {
        public ReviewEngineSettings Settings { get; set; } = new();

        public ReviewEngineAvailability Availability { get; set; } = new() { IsAvailable = true };

        public ReviewEngineValidationResult SaveResult { get; set; } = new();

        public ReviewEngineConnectionResult ConnectionResult { get; set; } = new() { Success = true };

        public ReviewEngineSettings SavedSettings { get; private set; }

        public ReviewEngineSettings TestedSettings { get; private set; }

        public Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Settings);
        }

        public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Availability.IsAvailable);
        }

        public ReviewEngineValidationResult Validate(ReviewEngineSettings settings)
        {
            return SaveResult;
        }

        public Task<ReviewEngineValidationResult> SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
        {
            if (SaveResult.IsValid)
            {
                SavedSettings = settings;
            }

            return Task.FromResult(SaveResult);
        }

        public Task<ReviewEngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Availability);
        }

        public Task<ReviewEngineConnectionResult> TestConnectionAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
        {
            TestedSettings = settings;
            return Task.FromResult(ConnectionResult);
        }
    }
}
