namespace Revathryn.Contracts.Models
{
    /// <summary>The active system and review-specific prompts resolved for a detection execution.</summary>
    public sealed class ResolvedPrompts
    {
        /// <summary>The active system prompt text.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The active review-specific prompt text.</summary>
        public string ReviewPrompt { get; set; } = string.Empty;

        /// <summary>The active system prompt version id, or null when no versioned prompt exists.</summary>
        public long? SystemPromptVersionId { get; set; }

        /// <summary>The active review-specific prompt version id, or null when no versioned prompt exists.</summary>
        public long? ReviewPromptVersionId { get; set; }
    }
}
