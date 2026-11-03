using System.Globalization;
using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>
    /// Assembles the analysis prompts for a prompt optimisation from the historical detection, validation and
    /// repository context. The original detection prompts are taken from the optimisation's stored snapshots,
    /// never from whatever is active now.
    /// </summary>
    public sealed class PromptOptimisationContextBuilder : IPromptOptimisationContextBuilder
    {
        private const string OriginalDetectionHeading = "# Original detection";
        private const string IndependentValidationHeading = "# Independent validation";
        private const string RepositoryContextHeading = "# Repository context";
        private const string PromptMetadataHeading = "# Prompt metadata";
        private const string PromptStart = "<<<";
        private const string PromptEnd = ">>>";
        private const string ListItemPrefix = "- ";
        private const string NotRecorded = "Not recorded";
        private const string None = "None";
        private const string Unknown = "Unknown";
        private const string LineEndingCarriageReturn = "\r";
        private const string NoValidationLine = "No completed independent validation is associated with this optimisation.";
        private const string NoSourceLine = "No source code is available.";
        private const string NoAdditionalContextLine = "No additional context was supplied to the detection engine.";
        private const string NoContextMetadataLine = "The original detection context was not stored.";
        private const string DescriptionLabel = "Description:";
        private const string DetectionRecommendationLabel = "Detection recommendation:";
        private const string ReasoningLabel = "Reasoning:";
        private const string SupportingEvidenceLabel = "Supporting evidence:";
        private const string LimitationsLabel = "Limitations and missing context:";
        private const string SuppliedSourceFileLabel = "Supplied source file:";
        private const string AdditionalContextLabel = "Additional context supplied to the detection engine:";
        private const string TruncatedMarker = " [TRUNCATED]";
        private const string DisagreementAnalysisHeading = "# Disagreement analysis";
        private const string OriginalSystemPromptLabel = "Original system prompt:";
        private const string OriginalReviewPromptLabel = "Original review-specific prompt:";
        private const string ConfidenceFormat = "F2";

        private readonly IFindingRepository _findingRepository;
        private readonly IIssueValidationRepository _validationRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IRepoFileRepository _fileRepository;

        /// <summary>Initialises a new context builder.</summary>
        public PromptOptimisationContextBuilder(
            IFindingRepository findingRepository,
            IIssueValidationRepository validationRepository,
            IReviewContextRepository reviewContextRepository,
            IRepositoryRepository repositoryRepository,
            IReviewTypeRepository reviewTypeRepository,
            IRepoFileRepository fileRepository)
        {
            _findingRepository = findingRepository;
            _validationRepository = validationRepository;
            _reviewContextRepository = reviewContextRepository;
            _repositoryRepository = repositoryRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _fileRepository = fileRepository;
        }

        /// <summary>Builds the prompt optimisation context for an optimisation record.</summary>
        public async Task<PromptOptimisationContext> BuildAsync(PromptOptimisation optimisation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);

            Finding finding = await _findingRepository.GetByIdAsync(optimisation.FindingId, cancellationToken).ConfigureAwait(false);
            IssueValidation validation = optimisation.IssueValidationId.HasValue
                ? await _validationRepository.GetByIdAsync(optimisation.IssueValidationId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            Repository repository = finding is not null
                ? await _repositoryRepository.GetByIdAsync(finding.RepositoryId, cancellationToken).ConfigureAwait(false)
                : null;
            ReviewType reviewType = finding?.ReviewTypeId.HasValue is true
                ? await _reviewTypeRepository.GetByIdAsync(finding.ReviewTypeId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            AIReviewContext original = finding?.ReviewContextId.HasValue is true
                ? await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            RepoFile file = finding?.RepoFileId.HasValue is true
                ? await _fileRepository.GetByIdAsync(finding.RepoFileId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            return new PromptOptimisationContext
            {
                AnalysisSystemPrompt = PromptAnalysisPrompts.SystemPrompt,
                ProposalSystemPrompt = PromptProposalPrompts.SystemPrompt,
                AnalysisUserPrompt = BuildUserPrompt(finding, validation, repository, reviewType, original, file, optimisation)
            };
        }

        /// <summary>Builds the stage 2 proposal user prompt from the completed analysis and the original prompts.</summary>
        public Task<string> BuildProposalUserPromptAsync(PromptOptimisation optimisation, PromptAnalysisResult analysis, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optimisation);
            ArgumentNullException.ThrowIfNull(analysis);

            return Task.FromResult(BuildProposalUserPrompt(optimisation, analysis));
        }

        private static string BuildUserPrompt(
            Finding finding,
            IssueValidation validation,
            Repository repository,
            ReviewType reviewType,
            AIReviewContext original,
            RepoFile file,
            PromptOptimisation optimisation)
        {
            StringBuilder builder = new();

            AppendOriginalDetection(builder, finding, reviewType, original);
            AppendValidation(builder, validation);
            AppendRepositoryContext(builder, repository, original, file);
            AppendPromptMetadata(builder, optimisation);

            return builder.ToString();
        }

        private static void AppendOriginalDetection(StringBuilder builder, Finding finding, ReviewType reviewType, AIReviewContext original)
        {
            builder.AppendLine(OriginalDetectionHeading);

            if (finding is null)
            {
                builder.AppendLine(NotRecorded);
                builder.AppendLine();
                return;
            }

            builder.AppendLine($"Finding id: {finding.Id}");
            builder.AppendLine($"Title: {finding.Title}");
            builder.AppendLine($"Review type: {reviewType?.Name ?? Unknown}");
            builder.AppendLine($"Severity: {finding.Severity}");
            builder.AppendLine($"Detection model: {ValueOrNotRecorded(original?.EngineModel)}");
            builder.AppendLine($"Detection engine: {ValueOrNotRecorded(original?.EngineName)}");
            builder.AppendLine($"Detection provider: {ValueOrNotRecorded(original?.EngineProvider)}");
            builder.AppendLine(DescriptionLabel);
            builder.AppendLine(finding.Description);
            builder.AppendLine(DetectionRecommendationLabel);
            builder.AppendLine(string.IsNullOrWhiteSpace(finding.SuggestedFix) ? None : finding.SuggestedFix);
            builder.AppendLine();
        }

        private static void AppendValidation(StringBuilder builder, IssueValidation validation)
        {
            builder.AppendLine(IndependentValidationHeading);

            if (validation is null)
            {
                builder.AppendLine(NoValidationLine);
                builder.AppendLine();
                return;
            }

            builder.AppendLine($"Validation id: {validation.Id}");
            builder.AppendLine($"Verdict: {validation.Verdict?.ToString() ?? NotRecorded}");
            builder.AppendLine($"Confidence: {FormatConfidence(validation.Confidence)}");
            builder.AppendLine($"Recommended severity: {validation.RecommendedSeverity?.ToString() ?? NotRecorded}");
            builder.AppendLine(ReasoningLabel);
            builder.AppendLine(string.IsNullOrWhiteSpace(validation.Reasoning) ? None : validation.Reasoning);

            IReadOnlyList<IssueValidationEvidence> evidence = IssueValidationJson.ReadEvidence(validation.EvidenceJson);

            if (evidence.Count > 0)
            {
                builder.AppendLine(SupportingEvidenceLabel);

                foreach (IssueValidationEvidence item in evidence)
                {
                    builder.AppendLine(string.Concat(
                        ListItemPrefix,
                        item.File,
                        item.LineStart > 0 ? $" (lines {item.LineStart}-{item.LineEnd})" : string.Empty,
                        string.IsNullOrWhiteSpace(item.Explanation) ? string.Empty : $": {item.Explanation}"));
                }
            }

            IReadOnlyList<string> limitations = IssueValidationJson.ReadStrings(validation.LimitationsJson);

            if (limitations.Count > 0)
            {
                builder.AppendLine(LimitationsLabel);

                foreach (string limitation in limitations)
                {
                    builder.AppendLine(string.Concat(ListItemPrefix, limitation));
                }
            }

            builder.AppendLine();
        }

        private static void AppendRepositoryContext(StringBuilder builder, Repository repository, AIReviewContext original, RepoFile file)
        {
            builder.AppendLine(RepositoryContextHeading);

            if (original is null)
            {
                builder.AppendLine(NoContextMetadataLine);
                builder.AppendLine();
                return;
            }

            builder.AppendLine($"Repository: {repository?.Name ?? Unknown}");
            builder.AppendLine($"File: {ValueOrNotRecorded(original.FilePath)}");
            builder.AppendLine($"Language: {ValueOrNotRecorded(original.Language)}");
            builder.AppendLine($"Context keys requested by the detection engine: {FormatRequestedKeys(original)}");

            if (string.IsNullOrWhiteSpace(original.FileContent))
            {
                builder.AppendLine(NoSourceLine);
            }
            else
            {
                builder.AppendLine(SuppliedSourceFileLabel);
                AppendDelimited(builder, original.FileContent);
            }

            if (original.ContextItems.Count == 0)
            {
                builder.AppendLine(NoAdditionalContextLine);
            }
            else
            {
                builder.AppendLine(AdditionalContextLabel);

                foreach (ReviewContextItem item in original.ContextItems)
                {
                    string truncation = item.Truncated ? TruncatedMarker : string.Empty;
                    string sourcePath = string.IsNullOrWhiteSpace(item.SourcePath) ? string.Empty : $" ({item.SourcePath})";
                    builder.AppendLine($"## {(string.IsNullOrWhiteSpace(item.Title) ? Unknown : item.Title)}{sourcePath}{truncation}");
                    builder.AppendLine(item.Content);
                }
            }

            _ = file;

            builder.AppendLine();
        }

        private static string BuildProposalUserPrompt(PromptOptimisation optimisation, PromptAnalysisResult analysis)
        {
            StringBuilder builder = new();

            builder.AppendLine(DisagreementAnalysisHeading);
            builder.AppendLine(PromptOptimisationJson.SerializeAnalysis(analysis));
            builder.AppendLine();
            builder.AppendLine(OriginalSystemPromptLabel);
            AppendDelimited(builder, optimisation.OriginalSystemPrompt);
            builder.AppendLine();
            builder.AppendLine(OriginalReviewPromptLabel);
            AppendDelimited(builder, optimisation.OriginalReviewPrompt);

            return builder.ToString();
        }

        private static void AppendPromptMetadata(StringBuilder builder, PromptOptimisation optimisation)
        {
            builder.AppendLine(PromptMetadataHeading);
            builder.AppendLine($"Original system prompt version id: {FormatVersion(optimisation.OriginalSystemPromptVersionId)}");
            builder.AppendLine(OriginalSystemPromptLabel);
            AppendDelimited(builder, optimisation.OriginalSystemPrompt);
            builder.AppendLine($"Original review prompt version id: {FormatVersion(optimisation.OriginalReviewPromptVersionId)}");
            builder.AppendLine(OriginalReviewPromptLabel);
            AppendDelimited(builder, optimisation.OriginalReviewPrompt);
        }

        private static string FormatVersion(long? versionId)
        {
            return versionId?.ToString(CultureInfo.InvariantCulture) ?? NotRecorded;
        }

        private static string FormatConfidence(double? confidence)
        {
            return confidence?.ToString(ConfidenceFormat, CultureInfo.InvariantCulture) ?? NotRecorded;
        }

        private static string FormatRequestedKeys(AIReviewContext original)
        {
            return original.RequestedContextKeys.Count == 0
                ? None
                : string.Join(Consts.ListSeparator, original.RequestedContextKeys);
        }

        private static void AppendDelimited(StringBuilder builder, string text)
        {
            builder.AppendLine(PromptStart);
            builder.AppendLine(string.IsNullOrWhiteSpace(text) ? NotRecorded : text);
            builder.AppendLine(PromptEnd);
        }

        private static string ValueOrNotRecorded(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? NotRecorded : value;
        }
    }
}
