using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Engine;

namespace Revathryn.Models.IssueValidations
{
    /// <summary>
    /// Presentation and API shape of a persisted issue validation. It deliberately excludes prompts,
    /// the raw provider response and engine configuration.
    /// </summary>
    public sealed class IssueValidationDto
    {
        /// <summary>Validation identifier.</summary>
        public long Id { get; init; }

        /// <summary>Validated issue identifier.</summary>
        public long FindingId { get; init; }

        /// <summary>Validation this one retried, if any.</summary>
        public long? RetryOfValidationId { get; init; }

        /// <summary>Execution status (Pending, Running, Completed or Failed).</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>True while the validation is pending or running.</summary>
        public bool IsActive { get; init; }

        /// <summary>True for the most recent validation of the issue.</summary>
        public bool IsLatest { get; init; }

        /// <summary>True when the validation failed and can be retried.</summary>
        public bool CanRetry { get; init; }

        /// <summary>Verdict, present only for completed validations.</summary>
        public string Verdict { get; init; }

        /// <summary>Normalised confidence between 0 and 1, when supplied by the review engine.</summary>
        public double? Confidence { get; init; }

        /// <summary>Review provider.</summary>
        public string Provider { get; init; } = string.Empty;

        /// <summary>Review model.</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>Creation time (UTC).</summary>
        public DateTime CreatedUtc { get; init; }

        /// <summary>Completion time (UTC).</summary>
        public DateTime? CompletedUtc { get; init; }

        /// <summary>Number of execution attempts.</summary>
        public int AttemptCount { get; init; }

        /// <summary>Title of the issue when the validation was requested.</summary>
        public string OriginalTitle { get; init; } = string.Empty;

        /// <summary>Description of the issue when the validation was requested.</summary>
        public string OriginalDescription { get; init; } = string.Empty;

        /// <summary>Severity of the issue when the validation was requested.</summary>
        public string OriginalSeverity { get; init; } = string.Empty;

        /// <summary>Severity recommended by the review engine, if any.</summary>
        public string RecommendedSeverity { get; init; }

        /// <summary>Explanation of the severity assessment.</summary>
        public string SeverityReasoning { get; init; } = string.Empty;

        /// <summary>Short conclusion.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Full explanation.</summary>
        public string Reasoning { get; init; } = string.Empty;

        /// <summary>Evidence references verified against the supplied context.</summary>
        public IReadOnlyList<IssueValidationEvidence> Evidence { get; init; } = [];

        /// <summary>Suggested corrections to the original finding.</summary>
        public IReadOnlyList<string> Suggestions { get; init; } = [];

        /// <summary>Limitations and missing context reported by the review engine.</summary>
        public IReadOnlyList<string> Limitations { get; init; } = [];

        /// <summary>How the context was assembled (OriginalSnapshot, CurrentSource or Unavailable).</summary>
        public string ContextMode { get; init; } = string.Empty;

        /// <summary>True when the supplied source differs from what the detection engine saw.</summary>
        public bool SourceChangedSinceDetection { get; init; }

        /// <summary>Notes explaining context limitations.</summary>
        public IReadOnlyList<string> ContextNotes { get; init; } = [];

        /// <summary>Failure message for failed validations, distinct from any verdict.</summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>Maps a persisted validation to its public shape.</summary>
        public static IssueValidationDto FromModel(IssueValidation validation, bool isLatest)
        {
            ArgumentNullException.ThrowIfNull(validation);

            IssueValidationContextReference reference = IssueValidationJson.ReadContextReference(validation.ContextReferenceJson);
            bool completed = validation.Status is IssueValidationStatus.Completed;

            return new IssueValidationDto
            {
                Id = validation.Id,
                FindingId = validation.FindingId,
                RetryOfValidationId = validation.RetryOfValidationId,
                Status = validation.Status.ToString(),
                IsActive = validation.Status is IssueValidationStatus.Pending or IssueValidationStatus.Running,
                IsLatest = isLatest,
                CanRetry = validation.Status is IssueValidationStatus.Failed,
                Verdict = completed ? validation.Verdict?.ToString() : null,
                Confidence = completed ? validation.Confidence : null,
                Provider = validation.Provider.ToString(),
                Model = validation.Model,
                CreatedUtc = validation.CreatedUtc,
                CompletedUtc = validation.CompletedUtc,
                AttemptCount = validation.AttemptCount,
                OriginalTitle = validation.OriginalTitle,
                OriginalDescription = validation.OriginalDescription,
                OriginalSeverity = validation.OriginalSeverity.ToString(),
                RecommendedSeverity = completed ? validation.RecommendedSeverity?.ToString() : null,
                SeverityReasoning = validation.SeverityReasoning,
                Summary = validation.Summary,
                Reasoning = validation.Reasoning,
                Evidence = IssueValidationJson.ReadEvidence(validation.EvidenceJson),
                Suggestions = IssueValidationJson.ReadStrings(validation.SuggestionsJson),
                Limitations = IssueValidationJson.ReadStrings(validation.LimitationsJson),
                ContextMode = reference?.Mode.ToString() ?? string.Empty,
                SourceChangedSinceDetection = reference?.SourceChangedSinceDetection ?? false,
                ContextNotes = reference?.Notes ?? [],
                ErrorMessage = validation.Status is IssueValidationStatus.Failed ? validation.ErrorMessage : string.Empty
            };
        }
    }
}
