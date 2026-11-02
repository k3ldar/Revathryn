using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ReviewEngineConfigurationServiceTests
    {
        private const string SecretName = "REVIEW_KEY";
        private const string SecretValue = "sk-super-secret-value";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task SaveAsync_ValidSettings_PersistsNormalisedSettings()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);
            ReviewEngineSettings settings = CreateValid();
            settings.Model = "  review-model  ";
            settings.ReasoningEffort = " HIGH ";

            ReviewEngineValidationResult result = await service.SaveAsync(settings, TestContext.CancellationToken);

            Assert.IsTrue(result.IsValid);
            ReviewEngineSettings saved = await fixture.Settings.GetAsync(TestContext.CancellationToken);
            Assert.IsNotNull(saved);
            Assert.AreEqual("review-model", saved.Model);
            Assert.AreEqual("high", saved.ReasoningEffort);
            Assert.AreEqual(SecretName, saved.SecretKeyName);
            Assert.IsTrue(saved.Enabled);
        }

        [TestMethod]
        public async Task SaveAsync_NeverStoresTheSecretValue()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out InMemorySecretsProvider secrets, out _);
            secrets.Secrets[SecretName] = SecretValue;

            _ = await service.SaveAsync(CreateValid(), TestContext.CancellationToken);

            ReviewEngineSettings saved = await fixture.Settings.GetAsync(TestContext.CancellationToken);
            Assert.DoesNotContain(SecretValue, System.Text.Json.JsonSerializer.Serialize(saved));
        }

        [TestMethod]
        public async Task SaveAsync_RepeatSaveReplacesTheSingleConfiguration()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            _ = await service.SaveAsync(CreateValid(), TestContext.CancellationToken);
            ReviewEngineSettings second = CreateValid();
            second.Model = "other-model";
            _ = await service.SaveAsync(second, TestContext.CancellationToken);

            Assert.AreEqual("other-model", (await fixture.Settings.GetAsync(TestContext.CancellationToken)).Model);
        }

        [TestMethod]
        public async Task SaveAsync_DoesNotSaveInvalidSettings()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);
            ReviewEngineSettings settings = CreateValid();
            settings.Model = string.Empty;

            ReviewEngineValidationResult result = await service.SaveAsync(settings, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.IsNull(await fixture.Settings.GetAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task SaveAsync_NullSettingsReportsNotConfigured()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            ReviewEngineValidationResult result = await service.SaveAsync(null, TestContext.CancellationToken);

            Assert.IsFalse(result.IsValid);
            Assert.Contains(Strings.ReviewEngineNotConfigured, result.Errors);
        }

        [TestMethod]
        public void Validate_ValidSettingsAreAccepted()
        {
            Assert.IsTrue(CreateService(null, out _, out _).Validate(CreateValid()).IsValid);
        }

        [TestMethod]
        public void Validate_LocalProviderDoesNotRequireSecretName()
        {
            ReviewEngineSettings settings = CreateValid();
            settings.Provider = AIProvider.Local;
            settings.SecretKeyName = string.Empty;

            Assert.IsTrue(CreateService(null, out _, out _).Validate(settings).IsValid);
        }

        [TestMethod]
        public void Validate_RejectsEachInvalidField()
        {
            ReviewEngineConfigurationService service = CreateService(null, out _, out _);

            AssertInvalid(service, s => s.Provider = (AIProvider)999, Strings.ReviewEngineProviderInvalid);
            AssertInvalid(service, s => s.Model = " ", Strings.ReviewEngineModelRequired);
            AssertInvalid(service, s => s.Endpoint = "not a url", Strings.ReviewEngineEndpointInvalid);
            AssertInvalid(service, s => s.Endpoint = "ftp://host/path", Strings.ReviewEngineEndpointInvalid);
            AssertInvalid(service, s => s.Endpoint = null, Strings.ReviewEngineEndpointInvalid);
            AssertInvalid(service, s => s.MaxTokens = 0, string.Format(Strings.ReviewEngineMaxTokensInvalid, Consts.MinimumAIMaxTokens));
            AssertInvalid(service, s => s.Temperature = -0.1, string.Format(Strings.ReviewEngineTemperatureInvalid, Consts.MinimumTemperature, Consts.MaximumTemperature));
            AssertInvalid(service, s => s.Temperature = 2.1, string.Format(Strings.ReviewEngineTemperatureInvalid, Consts.MinimumTemperature, Consts.MaximumTemperature));
            AssertInvalid(service, s => s.Temperature = double.NaN, string.Format(Strings.ReviewEngineTemperatureInvalid, Consts.MinimumTemperature, Consts.MaximumTemperature));
            AssertInvalid(service, s => s.ReasoningEffort = "extreme", null);
            AssertInvalid(service, s => s.SecretKeyName = string.Empty, Strings.ReviewEngineSecretKeyNameRequired);
            AssertInvalid(service, s => s.AdditionalOptions = "[1,2]", Strings.ReviewEngineAdditionalOptionsInvalid);
            AssertInvalid(service, s => s.AdditionalOptions = "{not json", null);
            AssertInvalid(service, s => s.AdditionalOptions = "{\"model\":\"x\"}", string.Format(Strings.ReviewEngineAdditionalOptionsReservedKey, "model"));
        }

        [TestMethod]
        public void Validate_BoundaryTemperaturesAreAccepted()
        {
            ReviewEngineConfigurationService service = CreateService(null, out _, out _);

            foreach (double temperature in new[] { Consts.MinimumTemperature, Consts.MaximumTemperature })
            {
                ReviewEngineSettings settings = CreateValid();
                settings.Temperature = temperature;
                Assert.IsTrue(service.Validate(settings).IsValid, temperature.ToString());
            }
        }

        [TestMethod]
        public void Validate_NullSettingsReportsNotConfigured()
        {
            ReviewEngineValidationResult result = CreateService(null, out _, out _).Validate(null);

            Assert.IsFalse(result.IsValid);
            Assert.Contains(Strings.ReviewEngineNotConfigured, result.Errors);
        }

        [TestMethod]
        public async Task GetAsync_ReturnsDisabledDefaultsWhenNothingSaved()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            ReviewEngineSettings settings = await service.GetAsync(TestContext.CancellationToken);

            Assert.IsFalse(settings.Enabled);
            Assert.AreEqual(Consts.DefaultReviewEngineEndpoint, settings.Endpoint);
            Assert.AreEqual(Consts.DefaultReviewEngineMaxTokens, settings.MaxTokens);
            Assert.IsFalse(await service.IsConfiguredAsync(TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task GetAvailabilityAsync_MissingConfigurationIsUnavailableWithReason()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            ReviewEngineAvailability availability = await service.GetAvailabilityAsync(TestContext.CancellationToken);

            Assert.IsFalse(availability.IsAvailable);
            Assert.AreEqual(Strings.ReviewEngineNotConfigured, availability.Reason);
        }

        [TestMethod]
        public async Task GetAvailabilityAsync_DisabledEngineIsUnavailable()
        {
            using ValidationTestFixture fixture = new();
            await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken, enabled: false);
            ReviewEngineConfigurationService service = CreateService(fixture, out InMemorySecretsProvider secrets, out _);
            secrets.Secrets[SecretName] = SecretValue;

            ReviewEngineAvailability availability = await service.GetAvailabilityAsync(TestContext.CancellationToken);

            Assert.IsFalse(availability.IsAvailable);
            Assert.AreEqual(Strings.ReviewEngineDisabled, availability.Reason);
        }

        [TestMethod]
        public async Task GetAvailabilityAsync_MissingSecretIsUnavailable()
        {
            using ValidationTestFixture fixture = new();
            await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken);
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            ReviewEngineAvailability availability = await service.GetAvailabilityAsync(TestContext.CancellationToken);

            Assert.IsFalse(availability.IsAvailable);
            Assert.AreEqual(Strings.ReviewEngineSecretMissing, availability.Reason);
        }

        [TestMethod]
        public async Task GetAvailabilityAsync_EnabledWithSecretIsAvailableAndLeaksNothing()
        {
            using ValidationTestFixture fixture = new();
            await fixture.SaveEnabledSettingsAsync(TestContext.CancellationToken);
            ReviewEngineConfigurationService service = CreateService(fixture, out InMemorySecretsProvider secrets, out _);
            secrets.Secrets[SecretName] = SecretValue;

            ReviewEngineAvailability availability = await service.GetAvailabilityAsync(TestContext.CancellationToken);

            Assert.IsTrue(availability.IsAvailable);
            Assert.DoesNotContain(SecretValue, availability.Reason);
        }

        [TestMethod]
        public async Task GetAvailabilityAsync_LocalProviderNeedsNoSecret()
        {
            using ValidationTestFixture fixture = new();
            await fixture.Settings.SaveAsync(
                new ReviewEngineSettings { Provider = AIProvider.Local, Endpoint = "http://localhost:1234/v1", Model = "local", MaxTokens = 512, Enabled = true },
                TestContext.CancellationToken);
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            Assert.IsTrue((await service.GetAvailabilityAsync(TestContext.CancellationToken)).IsAvailable);
        }

        [TestMethod]
        public async Task TestConnectionAsync_SuccessUsesSmallProbeWithoutJson()
        {
            ReviewEngineConfigurationService service = CreateService(null, out _, out ScriptedCompletionClient client);

            ReviewEngineConnectionResult result = await service.TestConnectionAsync(CreateValid(), TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(Strings.ReviewEngineConnectionSucceeded, result.Message);
            Assert.HasCount(1, client.Requests);
            Assert.AreEqual(Consts.ConnectionTestMaxTokens, client.Requests[0].Settings.MaxTokens);
            Assert.IsFalse(client.Requests[0].RequireJson);
            Assert.AreEqual("review-model", client.Requests[0].Settings.Model);
        }

        [TestMethod]
        public async Task TestConnectionAsync_ProviderFailureIsReportedWithoutSecret()
        {
            ReviewEngineConfigurationService service = CreateService(null, out _, out ScriptedCompletionClient client);
            client.Result = new AICompletionResult { Success = false, FailureKind = AICompletionFailureKind.Authentication, ErrorMessage = "401 Unauthorized" };

            ReviewEngineConnectionResult result = await service.TestConnectionAsync(CreateValid(), TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(string.Format(Strings.ReviewEngineConnectionFailed, "401 Unauthorized"), result.Message);
            Assert.DoesNotContain(SecretValue, result.Message);
        }

        [TestMethod]
        public async Task TestConnectionAsync_InvalidSettingsDoNotCallProvider()
        {
            ReviewEngineConfigurationService service = CreateService(null, out _, out ScriptedCompletionClient client);
            ReviewEngineSettings settings = CreateValid();
            settings.Model = string.Empty;

            ReviewEngineConnectionResult result = await service.TestConnectionAsync(settings, TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
            Assert.Contains(Strings.ReviewEngineModelRequired, result.Message);
            Assert.IsEmpty(client.Requests);
        }

        [TestMethod]
        public async Task TestConnectionAsync_DoesNotSaveSettings()
        {
            using ValidationTestFixture fixture = new();
            ReviewEngineConfigurationService service = CreateService(fixture, out _, out _);

            _ = await service.TestConnectionAsync(CreateValid(), TestContext.CancellationToken);

            Assert.IsNull(await fixture.Settings.GetAsync(TestContext.CancellationToken));
        }

        private static void AssertInvalid(ReviewEngineConfigurationService service, Action<ReviewEngineSettings> mutate, string expectedError)
        {
            ReviewEngineSettings settings = CreateValid();
            mutate(settings);

            ReviewEngineValidationResult result = service.Validate(settings);

            Assert.IsFalse(result.IsValid);

            if (expectedError is not null)
            {
                Assert.Contains(expectedError, result.Errors);
            }
        }

        private static ReviewEngineConfigurationService CreateService(
            ValidationTestFixture fixture,
            out InMemorySecretsProvider secrets,
            out ScriptedCompletionClient client)
        {
            secrets = new InMemorySecretsProvider();
            client = new ScriptedCompletionClient();
            return new ReviewEngineConfigurationService(fixture?.Settings, client, secrets);
        }

        private static ReviewEngineSettings CreateValid()
        {
            return new ReviewEngineSettings
            {
                Provider = AIProvider.OpenAI,
                Endpoint = "https://review.test/v1",
                Model = "review-model",
                Temperature = 0.2,
                ReasoningEffort = "medium",
                MaxTokens = 4096,
                SecretKeyName = SecretName,
                Enabled = true
            };
        }
    }
}
