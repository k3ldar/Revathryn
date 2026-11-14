using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Validates, saves and tests the independent review engine configuration.
    /// API keys are referenced by secret name only and are never returned or logged.
    /// </summary>
    public sealed class ReviewEngineConfigurationService : IReviewEngineConfigurationService
    {
        private static readonly string[] AllowedReasoningEfforts =
        [
            Consts.ReasoningEffortMinimal,
            Consts.ReasoningEffortLow,
            Consts.ReasoningEffortMedium,
            Consts.ReasoningEffortHigh
        ];

        private static readonly string[] ReservedOptionKeys =
        [
            Consts.RequestKeyModel,
            Consts.RequestKeyMessages,
            Consts.RequestKeyResponseFormat,
            Consts.RequestKeyMaxCompletionTokens,
            Consts.RequestKeyTemperature,
            Consts.RequestKeyReasoningEffort,
            Consts.RequestKeyStream
        ];

        private readonly IReviewEngineSettingsRepository _repository;
        private readonly IAICompletionClient _completionClient;
        private readonly ISecretsProvider _secretsProvider;

        /// <summary>Initialises a new configuration service.</summary>
        public ReviewEngineConfigurationService(
            IReviewEngineSettingsRepository repository,
            IAICompletionClient completionClient,
            ISecretsProvider secretsProvider)
        {
            _repository = repository;
            _completionClient = completionClient;
            _secretsProvider = secretsProvider;
        }

        /// <summary>Returns the saved settings, or defaults when none have been saved.</summary>
        public async Task<ReviewEngineSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            ReviewEngineSettings settings = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);
            return settings ?? CreateDefaults();
        }

        /// <summary>Returns true when settings have been saved.</summary>
        public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        {
            return await _repository.GetAsync(cancellationToken).ConfigureAwait(false) is not null;
        }

        /// <summary>Validates settings without saving them.</summary>
        public ReviewEngineValidationResult Validate(ReviewEngineSettings settings)
        {
            ReviewEngineValidationResult result = new();

            if (settings is null)
            {
                result.Errors.Add(Strings.ReviewEngineNotConfigured);
                return result;
            }

            if (!Enum.IsDefined(settings.Provider))
            {
                result.Errors.Add(Strings.ReviewEngineProviderInvalid);
            }

            if (string.IsNullOrWhiteSpace(settings.Model))
            {
                result.Errors.Add(Strings.ReviewEngineModelRequired);
            }

            if (!IsValidEndpoint(settings.Endpoint))
            {
                result.Errors.Add(Strings.ReviewEngineEndpointInvalid);
            }

            if (settings.MaxTokens < Consts.MinimumAIMaxTokens)
            {
                result.Errors.Add(string.Format(Strings.ReviewEngineMaxTokensInvalid, Consts.MinimumAIMaxTokens));
            }

            if (settings.MaxSimultaneousConnections is < Consts.MinimumAIMaxSimultaneousConnections or > Consts.MaximumAIMaxSimultaneousConnections)
            {
                result.Errors.Add(string.Format(Strings.ReviewEngineMaxSimultaneousConnectionsInvalid, Consts.MinimumAIMaxSimultaneousConnections, Consts.MaximumAIMaxSimultaneousConnections));
            }

            if (settings.Temperature.HasValue
                && (double.IsNaN(settings.Temperature.Value)
                    || settings.Temperature.Value < Consts.MinimumTemperature
                    || settings.Temperature.Value > Consts.MaximumTemperature))
            {
                result.Errors.Add(string.Format(Strings.ReviewEngineTemperatureInvalid, Consts.MinimumTemperature, Consts.MaximumTemperature));
            }

            if (!string.IsNullOrWhiteSpace(settings.ReasoningEffort)
                && !AllowedReasoningEfforts.Contains(settings.ReasoningEffort.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add(string.Format(Strings.ReviewEngineReasoningEffortInvalid, string.Join(Consts.ListSeparator, AllowedReasoningEfforts)));
            }

            if (settings.Provider is not AIProvider.Local && string.IsNullOrWhiteSpace(settings.SecretKeyName))
            {
                result.Errors.Add(Strings.ReviewEngineSecretKeyNameRequired);
            }

            ValidateAdditionalOptions(settings.AdditionalOptions, result);
            return result;
        }

        /// <summary>Validates and saves settings. Nothing is saved when validation fails.</summary>
        public async Task<ReviewEngineValidationResult> SaveAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
        {
            ReviewEngineValidationResult result = Validate(settings);

            if (!result.IsValid)
            {
                return result;
            }

            ReviewEngineSettings normalised = Normalise(settings);
            normalised.LastModifiedUtc = DateTime.UtcNow;

            await _repository.SaveAsync(normalised, cancellationToken).ConfigureAwait(false);
            return result;
        }

        /// <summary>Reports whether validation can currently be requested.</summary>
        public async Task<ReviewEngineAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            ReviewEngineSettings settings = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);

            if (settings is null)
            {
                return Unavailable(Strings.ReviewEngineNotConfigured);
            }

            if (!Validate(settings).IsValid)
            {
                return Unavailable(Strings.ReviewEngineConfigurationInvalid);
            }

            if (!settings.Enabled)
            {
                return Unavailable(Strings.ReviewEngineDisabled);
            }

            if (settings.Provider is not AIProvider.Local)
            {
                string key = await _secretsProvider.GetSecretAsync(settings.SecretKeyName, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(key))
                {
                    return Unavailable(Strings.ReviewEngineSecretMissing);
                }
            }

            return new ReviewEngineAvailability { IsAvailable = true };
        }

        /// <summary>Tests connectivity using the supplied settings. The result never contains credentials.</summary>
        public async Task<ReviewEngineConnectionResult> TestConnectionAsync(ReviewEngineSettings settings, CancellationToken cancellationToken = default)
        {
            ReviewEngineValidationResult validation = Validate(settings);

            if (!validation.IsValid)
            {
                return new ReviewEngineConnectionResult { Success = false, Message = string.Join(Consts.SpaceSeparator, validation.Errors) };
            }

            ReviewEngineSettings probe = Normalise(settings);
            probe.MaxTokens = Consts.ConnectionTestMaxTokens;

            AICompletionResult completion = await _completionClient.CompleteAsync(
                new AICompletionRequest
                {
                    Settings = probe,
                    SystemPrompt = Consts.ConnectionTestSystemPrompt,
                    UserPrompt = Consts.ConnectionTestUserPrompt,
                    RequireJson = false
                },
                cancellationToken).ConfigureAwait(false);

            return completion.Success
                ? new ReviewEngineConnectionResult { Success = true, Message = Strings.ReviewEngineConnectionSucceeded }
                : new ReviewEngineConnectionResult { Success = false, Message = string.Format(Strings.ReviewEngineConnectionFailed, completion.ErrorMessage) };
        }

        private static ReviewEngineSettings CreateDefaults()
        {
            return new ReviewEngineSettings
            {
                Provider = AIProvider.OpenAI,
                Endpoint = Consts.DefaultReviewEngineEndpoint,
                MaxTokens = Consts.DefaultReviewEngineMaxTokens,
                MaxSimultaneousConnections = Consts.DefaultAIMaxSimultaneousConnections,
                Enabled = false
            };
        }

        private static ReviewEngineSettings Normalise(ReviewEngineSettings settings)
        {
            return new ReviewEngineSettings
            {
                Provider = settings.Provider,
                Endpoint = settings.Endpoint.Trim(),
                Model = settings.Model.Trim(),
                Temperature = settings.Temperature,
                ReasoningEffort = (settings.ReasoningEffort ?? string.Empty).Trim().ToLowerInvariant(),
                MaxTokens = settings.MaxTokens,
                MaxSimultaneousConnections = settings.MaxSimultaneousConnections,
                SecretKeyName = (settings.SecretKeyName ?? string.Empty).Trim(),
                Enabled = settings.Enabled,
                AdditionalOptions = (settings.AdditionalOptions ?? string.Empty).Trim(),
                LastModifiedUtc = settings.LastModifiedUtc
            };
        }

        private static ReviewEngineAvailability Unavailable(string reason)
        {
            return new ReviewEngineAvailability { IsAvailable = false, Reason = reason };
        }

        private static bool IsValidEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static void ValidateAdditionalOptions(string additionalOptions, ReviewEngineValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(additionalOptions))
            {
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(additionalOptions);

                if (document.RootElement.ValueKind is not JsonValueKind.Object)
                {
                    result.Errors.Add(Strings.ReviewEngineAdditionalOptionsInvalid);
                    return;
                }

                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (ReservedOptionKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Errors.Add(string.Format(Strings.ReviewEngineAdditionalOptionsReservedKey, property.Name));
                    }
                }
            }
            catch (JsonException)
            {
                result.Errors.Add(Strings.ReviewEngineAdditionalOptionsInvalid);
            }
        }
    }
}
