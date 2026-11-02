namespace Revathryn.Contracts.Models
{
    /// <summary>The result of validating review engine configuration.</summary>
    public sealed class ReviewEngineValidationResult
    {
        /// <summary>True when there are no validation errors.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Validation errors, safe to display.</summary>
        public List<string> Errors { get; } = [];
    }
}
