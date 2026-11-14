using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A configured OpenAI-compatible AI engine.</summary>
    public class AIEngine
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Display name of the engine.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Provider flavour of the engine.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Base endpoint URL for the API.</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>Model identifier to use, e.g. "gpt-4o".</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Maximum tokens the model may generate in a single response.</summary>
        public int MaxTokens { get; set; } = Consts.DefaultAIMaxTokens;

        /// <summary>Maximum number of simultaneous requests sent to this engine.</summary>
        public int MaxSimultaneousConnections { get; set; } = Consts.DefaultAIMaxSimultaneousConnections;

        /// <summary>Reference into the secrets provider where the API key is stored.</summary>
        public string SecretKeyName { get; set; } = string.Empty;

        /// <summary>Whether the engine is enabled for use.</summary>
        public bool Enabled { get; set; }

        /// <summary>Whether this engine is the default when none is specified.</summary>
        public bool IsDefault { get; set; }

        /// <summary>Whether reviews using this engine are primed with a cached descriptive structure summary.</summary>
        public bool UseArchitecturePass { get; set; }
    }
}
