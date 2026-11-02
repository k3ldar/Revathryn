using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>The prompts and context reference assembled for a validation.</summary>
    public sealed class IssueValidationContext
    {
        /// <summary>The dedicated validation system prompt.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt containing the finding, detection metadata and supplied context.</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>The version of the validation prompt.</summary>
        public string PromptVersion { get; set; } = string.Empty;

        /// <summary>Description of the context supplied.</summary>
        public IssueValidationContextReference Reference { get; set; } = new();

        /// <summary>Source files supplied, keyed by repository-relative path, with the line count supplied.</summary>
        public Dictionary<string, int> SuppliedSources { get; set; } = [];
    }
}
