namespace Revathryn.Contracts.Models
{
    /// <summary>The original context supplied to the AI during a single review execution.</summary>
    public sealed class AIReviewContext
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The reviewed file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type that produced the execution.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>SHA256 fingerprint of the file content at review time.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Language of the reviewed file.</summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the reviewed file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>The complete instructions supplied to the AI.</summary>
        public string ReviewInstructions { get; set; } = string.Empty;

        /// <summary>The system prompt that formed part of the instructions. Empty for contexts captured before this was recorded.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The review-specific prompt that formed part of the instructions. Empty for contexts captured before this was recorded.</summary>
        public string ReviewPrompt { get; set; } = string.Empty;

        /// <summary>Version identity of the system prompt used at detection time. Null when not versioned.</summary>
        public long? SystemPromptVersionId { get; set; }

        /// <summary>Version identity of the review-specific prompt used at detection time. Null when not versioned.</summary>
        public long? ReviewPromptVersionId { get; set; }

        /// <summary>Version of the review definition at detection time. Empty when not recorded.</summary>
        public string ReviewDefinitionVersion { get; set; } = string.Empty;

        /// <summary>Name of the detection engine. Empty when not recorded.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Provider of the detection engine. Empty when not recorded.</summary>
        public string EngineProvider { get; set; } = string.Empty;

        /// <summary>Model of the detection engine. Empty when not recorded.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Context keys the detection engine requested, which may differ from what was supplied.</summary>
        public IReadOnlyList<string> RequestedContextKeys { get; set; } = [];

        /// <summary>The reviewed file content supplied to the AI.</summary>
        public string FileContent { get; set; } = string.Empty;

        /// <summary>Additional context items supplied to the AI, preserved as supplied.</summary>
        public IReadOnlyList<ReviewContextItem> ContextItems { get; set; } = [];

        /// <summary>UTC timestamp when the review was completed.</summary>
        public DateTime CreatedUtc { get; set; }
    }
}
