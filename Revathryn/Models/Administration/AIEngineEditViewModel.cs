using System.ComponentModel.DataAnnotations;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>Form model for creating and editing an AI engine.</summary>
    public sealed class AIEngineEditViewModel
    {
        /// <summary>Engine identifier; zero when creating.</summary>
        public long Id { get; set; }

        /// <summary>Display name of the engine.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.NameRequiredMessage))]
        public string Name { get; set; } = string.Empty;

        /// <summary>Provider flavour of the engine.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Base endpoint URL for the API.</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>Model identifier to use.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.ModelRequiredMessage))]
        public string Model { get; set; } = string.Empty;

        /// <summary>Maximum tokens the model may generate in a single response.</summary>
        [Range(Consts.MinimumAIMaxTokens, int.MaxValue, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.MaxTokensRangeMessage))]
        public int MaxTokens { get; set; } = Consts.DefaultAIMaxTokens;

        /// <summary>Maximum number of simultaneous requests sent to this engine.</summary>
        [Range(Consts.MinimumAIMaxSimultaneousConnections, Consts.MaximumAIMaxSimultaneousConnections, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.MaxSimultaneousConnectionsRangeMessage))]
        public int MaxSimultaneousConnections { get; set; } = Consts.DefaultAIMaxSimultaneousConnections;

        /// <summary>Reference into the secrets provider where the API key is stored.</summary>
        public string SecretKeyName { get; set; } = string.Empty;

        /// <summary>Whether the engine is enabled for use.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether this engine is the default when none is specified.</summary>
        public bool IsDefault { get; set; }

        /// <summary>Whether reviews using this engine are primed with a cached descriptive structure summary.</summary>
        public bool UseArchitecturePass { get; set; }

        /// <summary>Creates a form model from a persisted engine.</summary>
        public static AIEngineEditViewModel FromEngine(AIEngine engine)
        {
            return new AIEngineEditViewModel
            {
                Id = engine.Id,
                Name = engine.Name,
                Provider = engine.Provider,
                Endpoint = engine.Endpoint,
                Model = engine.Model,
                MaxTokens = engine.MaxTokens,
                MaxSimultaneousConnections = engine.MaxSimultaneousConnections,
                SecretKeyName = engine.SecretKeyName,
                Enabled = engine.Enabled,
                IsDefault = engine.IsDefault,
                UseArchitecturePass = engine.UseArchitecturePass
            };
        }

        /// <summary>Converts the form model to a persisted engine.</summary>
        public AIEngine ToEngine()
        {
            return new AIEngine
            {
                Id = Id,
                Name = Name?.Trim() ?? string.Empty,
                Provider = Provider,
                Endpoint = Endpoint?.Trim() ?? string.Empty,
                Model = Model?.Trim() ?? string.Empty,
                MaxTokens = MaxTokens,
                MaxSimultaneousConnections = MaxSimultaneousConnections,
                SecretKeyName = SecretKeyName?.Trim() ?? string.Empty,
                Enabled = Enabled,
                IsDefault = IsDefault,
                UseArchitecturePass = UseArchitecturePass
            };
        }
    }
}
