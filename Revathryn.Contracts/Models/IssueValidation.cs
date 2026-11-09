using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// A single independent validation of a finding. Each execution is a separate, immutable
    /// historical record; it never modifies the finding it validates.
    /// </summary>
    public sealed class IssueValidation
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The validated finding.</summary>
        public long FindingId { get; set; }

        /// <summary>The repository owning the finding.</summary>
        public long RepositoryId { get; set; }

        /// <summary>Execution state. Distinct from the finding lifecycle status.</summary>
        public IssueValidationStatus Status { get; set; }

        /// <summary>The failed validation this record retries, when applicable.</summary>
        public long? RetryOfValidationId { get; set; }

        /// <summary>Whether this validation was queued automatically by the re-review pipeline.</summary>
        public bool IsAutomatic { get; set; }

        /// <summary>Number of execution attempts made.</summary>
        public int AttemptCount { get; set; }

        /// <summary>UTC timestamp when the validation was requested.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>UTC timestamp when execution first started.</summary>
        public DateTime? StartedUtc { get; set; }

        /// <summary>UTC timestamp when the validation completed or failed.</summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>UTC time before which a pending retry must not be claimed.</summary>
        public DateTime? NextAttemptUtc { get; set; }

        /// <summary>UTC time at which the current execution lease expires.</summary>
        public DateTime? LeasedUntilUtc { get; set; }

        /// <summary>Review engine provider used.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Review engine model used.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Non-secret review engine configuration as JSON (temperature, effort, limits, options).</summary>
        public string EngineConfigSnapshotJson { get; set; } = string.Empty;

        /// <summary>Version of the validation prompt used.</summary>
        public string PromptVersion { get; set; } = string.Empty;

        /// <summary>The system prompt actually sent to the review engine.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>The user prompt actually sent to the review engine, including the supplied context.</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>Description of the context supplied, as JSON of <see cref="IssueValidationContextReference"/>.</summary>
        public string ContextReferenceJson { get; set; } = string.Empty;

        /// <summary>Severity of the finding when the validation was requested.</summary>
        public FindingSeverity OriginalSeverity { get; set; }

        /// <summary>Title of the finding when the validation was requested.</summary>
        public string OriginalTitle { get; set; } = string.Empty;

        /// <summary>Description of the finding when the validation was requested.</summary>
        public string OriginalDescription { get; set; } = string.Empty;

        /// <summary>Suggested fix of the finding when the validation was requested.</summary>
        public string OriginalSuggestedFix { get; set; } = string.Empty;

        /// <summary>The verdict. Null unless the validation completed.</summary>
        public IssueValidationVerdict? Verdict { get; set; }

        /// <summary>Normalised confidence between 0 and 1, or null when the model did not supply one.</summary>
        public double? Confidence { get; set; }

        /// <summary>Short conclusion.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Full explanation of the conclusion.</summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>Severity recommended by the review engine, when supplied.</summary>
        public FindingSeverity? RecommendedSeverity { get; set; }

        /// <summary>Explanation of the severity recommendation.</summary>
        public string SeverityReasoning { get; set; } = string.Empty;

        /// <summary>Evidence as JSON of a list of <see cref="IssueValidationEvidence"/>.</summary>
        public string EvidenceJson { get; set; } = string.Empty;

        /// <summary>Suggested corrections as a JSON array of strings.</summary>
        public string SuggestionsJson { get; set; } = string.Empty;

        /// <summary>Limitations and missing context as a JSON array of strings.</summary>
        public string LimitationsJson { get; set; } = string.Empty;

        /// <summary>Raw response from the review engine, retained for diagnostics only.</summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>Description of the failure when <see cref="Status"/> is failed.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Failure classification when <see cref="Status"/> is failed.</summary>
        public FailureCategory? FailureClassification { get; set; }

        /// <summary>Prompt tokens consumed across all attempts.</summary>
        public int PromptTokens { get; set; }

        /// <summary>Completion tokens consumed across all attempts.</summary>
        public int CompletionTokens { get; set; }

        /// <summary>Total tokens consumed across all attempts.</summary>
        public int TotalTokens { get; set; }
    }
}
