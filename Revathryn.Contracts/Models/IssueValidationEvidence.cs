namespace Revathryn.Contracts.Models
{
    /// <summary>A reference to supplied source code that supports a validation conclusion.</summary>
    public sealed class IssueValidationEvidence
    {
        /// <summary>Repository-relative file path of the evidence.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>First line of the evidence, or 0 when not specified.</summary>
        public int LineStart { get; set; }

        /// <summary>Last line of the evidence, or 0 when not specified.</summary>
        public int LineEnd { get; set; }

        /// <summary>Explanation of why the referenced code is relevant.</summary>
        public string Explanation { get; set; } = string.Empty;
    }
}
