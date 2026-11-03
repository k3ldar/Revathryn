namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// Basic prompt effectiveness statistics. Agreement between AI models is an indicator, not ground truth;
    /// these figures must be presented as AI validation statistics unless a human confirmation exists.
    /// </summary>
    public sealed class PromptStatistics
    {
        /// <summary>Number of issues detected under the matching prompt versions.</summary>
        public int IssuesDetected { get; set; }

        /// <summary>Number of independently validated issues under the matching prompt versions.</summary>
        public int ValidatedIssues { get; set; }

        /// <summary>Count of agreed validations.</summary>
        public int AgreedCount { get; set; }

        /// <summary>Count of disagreed validations.</summary>
        public int DisagreedCount { get; set; }

        /// <summary>Count of partially agreed validations.</summary>
        public int PartiallyAgreedCount { get; set; }

        /// <summary>Count of inconclusive validations.</summary>
        public int InconclusiveCount { get; set; }

        /// <summary>Agreement rate as a percentage (0-100), or null when no validations exist.</summary>
        public double? AgreementRate { get; set; }

        /// <summary>Disagreement rate as a percentage (0-100), or null when no validations exist.</summary>
        public double? DisagreementRate { get; set; }

        /// <summary>Partial agreement rate as a percentage (0-100), or null when no validations exist.</summary>
        public double? PartialAgreementRate { get; set; }

        /// <summary>Inconclusive rate as a percentage (0-100), or null when no validations exist.</summary>
        public double? InconclusiveRate { get; set; }

        /// <summary>Number of prompt test runs matching the filter.</summary>
        public int PromptTests { get; set; }

        /// <summary>Number of prompt version activations matching the filter.</summary>
        public int PromptActivations { get; set; }
    }
}
