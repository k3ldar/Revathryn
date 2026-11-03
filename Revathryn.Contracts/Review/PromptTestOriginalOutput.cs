using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Review
{
    /// <summary>The original detection output retained for comparison against a prompt test run.</summary>
    public sealed class PromptTestOriginalOutput
    {
        /// <summary>The original issue id.</summary>
        public long FindingId { get; set; }

        /// <summary>Severity of the original issue.</summary>
        public FindingSeverity Severity { get; set; }

        /// <summary>Title of the original issue.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Description of the original issue.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Suggested fix of the original issue.</summary>
        public string SuggestedFix { get; set; } = string.Empty;

        /// <summary>Detection engine name, when recorded.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Detection engine provider, when recorded.</summary>
        public string EngineProvider { get; set; } = string.Empty;

        /// <summary>Detection engine model, when recorded.</summary>
        public string EngineModel { get; set; } = string.Empty;
    }
}
