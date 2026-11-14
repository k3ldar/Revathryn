namespace Revathryn.Models.Api
{
    /// <summary>
    /// Non-sensitive summary of the AI context supplied to a review. It deliberately excludes
    /// prompts and the reviewed source content.
    /// </summary>
    public sealed class FileReviewContextSummary
    {
        /// <summary>Context identifier.</summary>
        public long Id { get; init; }

        /// <summary>Display name of the detection engine.</summary>
        public string EngineName { get; init; } = string.Empty;

        /// <summary>Provider of the detection engine.</summary>
        public string EngineProvider { get; init; } = string.Empty;

        /// <summary>Model of the detection engine.</summary>
        public string EngineModel { get; init; } = string.Empty;

        /// <summary>Language of the reviewed file.</summary>
        public string Language { get; init; } = string.Empty;

        /// <summary>Version of the review definition at detection time.</summary>
        public string ReviewDefinitionVersion { get; init; } = string.Empty;

        /// <summary>Context keys the detection engine requested.</summary>
        public IReadOnlyList<string> RequestedContextKeys { get; init; } = [];

        /// <summary>Git branch the review was completed against.</summary>
        public string Branch { get; init; } = string.Empty;

        /// <summary>UTC timestamp when the review completed.</summary>
        public DateTime CreatedUtc { get; init; }
    }
}
