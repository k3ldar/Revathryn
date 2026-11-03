using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// A guided prompt optimisation analysis initiated from an issue validation. The review engine investigates
    /// a disagreement, recommends changes to one or both prompts, and stores those recommendations for user review.
    /// It never activates a prompt and never modifies the originating issue or validation.
    /// </summary>
    public sealed class PromptOptimisation
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The completed validation this optimisation originated from, when applicable.</summary>
        public long? IssueValidationId { get; set; }

        /// <summary>The finding whose validation prompted this optimisation.</summary>
        public long FindingId { get; set; }

        /// <summary>The repository owning the finding.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The review type being investigated, or null for system-prompt-only analysis.</summary>
        public long? ReviewTypeId { get; set; }

        /// <summary>Execution state.</summary>
        public PromptOptimisationStatus Status { get; set; }

        /// <summary>The failed optimisation this record retries, when applicable.</summary>
        public long? RetryOfOptimisationId { get; set; }

        /// <summary>Number of execution attempts made.</summary>
        public int AttemptCount { get; set; }

        /// <summary>UTC timestamp when the optimisation was requested.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>UTC timestamp when execution first started.</summary>
        public DateTime? StartedUtc { get; set; }

        /// <summary>UTC timestamp when the optimisation completed or failed.</summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>UTC time before which a pending retry must not be claimed.</summary>
        public DateTime? NextAttemptUtc { get; set; }

        /// <summary>UTC time at which the current execution lease expires.</summary>
        public DateTime? LeasedUntilUtc { get; set; }

        /// <summary>Review engine provider used.</summary>
        public AIProvider Provider { get; set; }

        /// <summary>Review engine model used.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Non-secret review engine configuration as JSON.</summary>
        public string EngineConfigSnapshotJson { get; set; } = string.Empty;

        /// <summary>The exact system prompt used by the detection engine for the original finding.</summary>
        public string OriginalSystemPrompt { get; set; } = string.Empty;

        /// <summary>The exact review-specific prompt used by the detection engine for the original finding.</summary>
        public string OriginalReviewPrompt { get; set; } = string.Empty;

        /// <summary>Version identity of the original system prompt, when known.</summary>
        public long? OriginalSystemPromptVersionId { get; set; }

        /// <summary>Version identity of the original review-specific prompt, when known.</summary>
        public long? OriginalReviewPromptVersionId { get; set; }

        /// <summary>The structured disagreement analysis as JSON of <see cref="Review.PromptAnalysisResult"/>.</summary>
        public string AnalysisJson { get; set; } = string.Empty;

        /// <summary>The generated system prompt recommendation metadata as JSON, when one was produced.</summary>
        public string SystemPromptRecommendationJson { get; set; } = string.Empty;

        /// <summary>The generated review prompt recommendation metadata as JSON, when one was produced.</summary>
        public string ReviewPromptRecommendationJson { get; set; } = string.Empty;

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
