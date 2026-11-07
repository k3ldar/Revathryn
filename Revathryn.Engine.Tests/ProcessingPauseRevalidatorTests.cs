using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Engine.Reliability;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ProcessingPauseRevalidatorTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TryResumeAsync_ResumesWhenConfigurationIsAvailable()
        {
            ProcessingGate gate = new();
            gate.Pause("misconfigured");
            FakeConfigurationService configuration = new() { Availability = new ReviewEngineAvailability { IsAvailable = true } };
            ProcessingPauseRevalidator revalidator = new(gate, configuration);

            bool resumed = await revalidator.TryResumeAsync(TestContext.CancellationToken);

            Assert.IsTrue(resumed);
            Assert.IsFalse(gate.IsPaused);
            Assert.AreEqual(string.Empty, gate.Reason);
        }

        [TestMethod]
        public async Task TryResumeAsync_StaysPausedWhenConfigurationUnavailable()
        {
            ProcessingGate gate = new();
            gate.Pause("misconfigured");
            FakeConfigurationService configuration = new() { Availability = new ReviewEngineAvailability { IsAvailable = false } };
            ProcessingPauseRevalidator revalidator = new(gate, configuration);

            bool resumed = await revalidator.TryResumeAsync(TestContext.CancellationToken);

            Assert.IsFalse(resumed);
            Assert.IsTrue(gate.IsPaused);
            Assert.AreEqual("misconfigured", gate.Reason);
        }

        [TestMethod]
        public async Task TryResumeAsync_ReturnsFalseWithoutCheckingWhenNotPaused()
        {
            ProcessingGate gate = new();
            FakeConfigurationService configuration = new();
            ProcessingPauseRevalidator revalidator = new(gate, configuration);

            bool resumed = await revalidator.TryResumeAsync(TestContext.CancellationToken);

            Assert.IsFalse(resumed);
            Assert.AreEqual(0, configuration.GetAvailabilityCallCount);
        }

        private sealed class FakeConfigurationService : IReviewEngineConfigurationService
        {
            public ReviewEngineAvailability Availability { get; set; } = new();

            public int GetAvailabilityCallCount { get; private set; }

            public Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ReviewEngineSettings>(null);
            }

            public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }

            public ReviewEngineValidationResult Validate(ReviewEngineSettings settings)
            {
                return new ReviewEngineValidationResult();
            }

            public Task<ReviewEngineValidationResult> SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReviewEngineValidationResult());
            }

            public Task<ReviewEngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
            {
                GetAvailabilityCallCount++;
                return Task.FromResult(Availability);
            }

            public Task<ReviewEngineConnectionResult> TestConnectionAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReviewEngineConnectionResult());
            }
        }
    }
}
