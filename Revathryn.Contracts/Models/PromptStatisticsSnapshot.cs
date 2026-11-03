namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// Raw counts aggregated by <see cref="Persistence.IPromptStatisticsRepository"/>. Rates are computed by
    /// the service layer so that the repository remains a pure aggregation boundary.
    /// </summary>
    public sealed class PromptStatisticsSnapshot
    {
        /// <summary>Number of issues detected under the matching prompt versions.</summary>
        public int IssuesDetected { get; set; }

        /// <summary>Number of findings with a completed validation under the matching prompt versions.</summary>
        public int ValidatedIssues { get; set; }

        /// <summary>Count of agreed validations.</summary>
        public int AgreedCount { get; set; }

        /// <summary>Count of disagreed validations.</summary>
        public int DisagreedCount { get; set; }

        /// <summary>Count of partially agreed validations.</summary>
        public int PartiallyAgreedCount { get; set; }

        /// <summary>Count of inconclusive validations.</summary>
        public int InconclusiveCount { get; set; }

        /// <summary>Number of prompt test runs matching the filter.</summary>
        public int PromptTests { get; set; }

        /// <summary>Number of prompt version activations matching the filter.</summary>
        public int PromptActivations { get; set; }
    }
}
