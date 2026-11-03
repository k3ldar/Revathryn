using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A logical prompt identity. Its content lives in immutable <see cref="PromptVersion"/> rows.</summary>
    public sealed class Prompt
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Whether this is the global system prompt or a review-specific prompt.</summary>
        public PromptType PromptType { get; set; }

        /// <summary>The review type this prompt belongs to, or null for the system prompt.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>UTC timestamp when the prompt identity was created.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>UTC timestamp when the prompt identity was last modified.</summary>
        public DateTime LastModifiedUtc { get; set; }
    }
}
