using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// An isolated prompt test execution. It replays the original detection scenario with a proposed prompt and
    /// stores the comparison separately from production issues. It never creates or modifies production findings.
    /// </summary>
    public sealed class PromptTestRun
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The prompt version being tested.</summary>
        public long PromptVersionId { get; set; }

        /// <summary>The review type the test applies to. Null for a system-prompt-only test.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>The originating issue, when the test replays a specific detection scenario.</summary>
        public long? OriginalIssueId { get; set; }

        /// <summary>Detection model used, or empty when the original model configuration was unavailable.</summary>
        public string DetectionModel { get; set; } = string.Empty;

        /// <summary>Reference to the original detection context that was replayed, when available.</summary>
        public long? ReviewContextId { get; set; }

        /// <summary>Description of the context supplied as JSON.</summary>
        public string ContextSnapshotJson { get; set; } = string.Empty;

        /// <summary>Exact prompt text tested.</summary>
        public string PromptText { get; set; } = string.Empty;

        /// <summary>The original detection output as JSON, when available.</summary>
        public string OriginalOutputJson { get; set; } = string.Empty;

        /// <summary>The revised detection output as JSON.</summary>
        public string TestOutputJson { get; set; } = string.Empty;

        /// <summary>Structured differences between the two outputs as JSON.</summary>
        public string ComparisonJson { get; set; } = string.Empty;

        /// <summary>Execution state.</summary>
        public PromptTestRunStatus Status { get; set; }

        /// <summary>Number of execution attempts made.</summary>
        public int AttemptCount { get; set; }

        /// <summary>UTC timestamp when the test was requested.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>UTC timestamp when execution first started.</summary>
        public DateTime? StartedUtc { get; set; }

        /// <summary>UTC timestamp when the test completed or failed.</summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>UTC time before which a pending retry must not be claimed.</summary>
        public DateTime? NextAttemptUtc { get; set; }

        /// <summary>UTC time at which the current execution lease expires.</summary>
        public DateTime? LeasedUntilUtc { get; set; }

        /// <summary>Detection engine provider used.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Detection engine model used.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Non-secret detection engine configuration as JSON.</summary>
        public string EngineConfigSnapshotJson { get; set; } = string.Empty;

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
