using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Analysis
{
    /// <summary>Outcome of a descriptive architecture pass.</summary>
    public sealed class ArchitectureAnalysisResponse
    {
        /// <summary>Whether the request completed without an operational error.</summary>
        public bool Success { get; set; }

        /// <summary>Whether the pass was skipped because the resolved engine has the pass disabled.</summary>
        public bool Skipped { get; set; }

        /// <summary>Optional error message when <see cref="Success"/> is false.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Free-text structural summary produced by the model.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Raw provider response body, preserved for failure diagnostics.</summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>Identifier of the engine that served the pass.</summary>
        public long EngineId { get; set; }

        /// <summary>Display name of the engine that served the pass.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Provider of the engine that served the pass.</summary>
        public string EngineProvider { get; set; } = string.Empty;

        /// <summary>Model identifier used by the engine.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Token usage consumed by the pass.</summary>
        public TokenUsage Usage { get; set; }
    }
}
