using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Review
{
    /// <summary>Everything the review engine needs to independently validate a finding.</summary>
    public sealed class IssueValidationRequest
    {
        /// <summary>The review engine configuration to use.</summary>
        public ReviewEngineSettings Settings { get; set; }

        /// <summary>The dedicated validation system prompt.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt containing the finding, detection metadata and supplied context.</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Source files that were supplied to the review engine, keyed by repository-relative path,
        /// with the number of lines supplied. Evidence must reference these files.
        /// </summary>
        public IReadOnlyDictionary<string, int> SuppliedSources { get; set; } = new Dictionary<string, int>();
    }
}
