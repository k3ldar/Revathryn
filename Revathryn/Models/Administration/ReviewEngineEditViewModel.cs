using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>Edit model for the independent review engine. The API key itself is never part of this model.</summary>
    public sealed class ReviewEngineEditViewModel
    {
        /// <summary>AI provider used for independent validation.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Provider endpoint.</summary>
        public string Endpoint { get; set; } = Consts.DefaultReviewEngineEndpoint;

        /// <summary>Model identifier.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Sampling temperature, or empty when the model does not support it.</summary>
        public double? Temperature { get; set; }

        /// <summary>Reasoning effort, or empty when not supported.</summary>
        public string ReasoningEffort { get; set; } = string.Empty;

        /// <summary>Maximum output tokens.</summary>
        public int MaxTokens { get; set; } = Consts.DefaultReviewEngineMaxTokens;

        /// <summary>Maximum number of simultaneous requests sent to this engine.</summary>
        public int MaxSimultaneousConnections { get; set; } = Consts.DefaultAIMaxSimultaneousConnections;

        /// <summary>Name of the secret (environment variable) holding the API key.</summary>
        public string SecretKeyName { get; set; } = string.Empty;

        /// <summary>Whether re-review is available.</summary>
        public bool Enabled { get; set; }

        /// <summary>Provider-specific options as a JSON object.</summary>
        public string AdditionalOptions { get; set; } = string.Empty;

        /// <summary>Creates the edit model from stored settings.</summary>
        public static ReviewEngineEditViewModel FromSettings(ReviewEngineSettings settings)
        {
            return new ReviewEngineEditViewModel
            {
                Provider = settings.Provider,
                Endpoint = settings.Endpoint,
                Model = settings.Model,
                Temperature = settings.Temperature,
                ReasoningEffort = settings.ReasoningEffort,
                MaxTokens = settings.MaxTokens,
                MaxSimultaneousConnections = settings.MaxSimultaneousConnections,
                SecretKeyName = settings.SecretKeyName,
                Enabled = settings.Enabled,
                AdditionalOptions = settings.AdditionalOptions
            };
        }

        /// <summary>Converts the edit model to settings for validation and saving.</summary>
        public ReviewEngineSettings ToSettings()
        {
            return new ReviewEngineSettings
            {
                Provider = Provider,
                Endpoint = Endpoint?.Trim() ?? string.Empty,
                Model = Model?.Trim() ?? string.Empty,
                Temperature = Temperature,
                ReasoningEffort = ReasoningEffort?.Trim() ?? string.Empty,
                MaxTokens = MaxTokens,
                MaxSimultaneousConnections = MaxSimultaneousConnections,
                SecretKeyName = SecretKeyName?.Trim() ?? string.Empty,
                Enabled = Enabled,
                AdditionalOptions = AdditionalOptions?.Trim() ?? string.Empty
            };
        }
    }
}
