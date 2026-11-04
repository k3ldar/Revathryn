using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Assembles the prompts for independent validation. The original detection context is reused where it
    /// was stored, current source is never silently presented as the original context, and every gap is
    /// reported to the review engine and recorded against the validation.
    /// </summary>
    public sealed class IssueValidationContextBuilder : IIssueValidationContextBuilder
    {
        private const string FindingHeading = "# Original finding";
        private const string DetectionHeading = "# Detection metadata";
        private const string SourceHeading = "# Source supplied to the review engine";
        private const string AdditionalContextHeading = "# Additional context supplied to the detection engine";
        private const string NotesHeading = "# Context notes";
        private const string PromptStart = "<<<";
        private const string PromptEnd = ">>>";
        private const string LineNumberSeparator = ": ";
        private const string ListItemPrefix = "- ";
        private const string NotRecorded = "Not recorded";
        private const string None = "None";
        private const string Unknown = "Unknown";
        private const string LineEndingCarriageReturn = "\r";
        private const string LineReferencesLine = "Line references: Not recorded by the detection engine";
        private const string DescriptionLine = "Description:";
        private const string RecommendationLine = "Recommendation from the detection engine:";
        private const string EvidenceNotRecordedLine = "Original evidence and reasoning: the detection engine recorded no separate evidence or reasoning; the description above is its only explanation.";
        private const string DetectionContextNotStoredLine = "The original detection context was not stored for this finding. Detection engine, prompts and supplied context are unknown.";
        private const string DetectionInstructionsLine = "Original detection instructions (the complete system prompt as sent):";
        private const string ReviewPromptLine = "Review-specific prompt:";
        private const string NoSourceLine = "No source code is available.";
        private const string OriginalSnapshotOrigin = "original snapshot supplied to the detection engine";
        private const string CurrentSourceOrigin = "CURRENT repository source, which is NOT necessarily what the detection engine saw";

        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IRepoFileRepository _fileRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IReviewContextRepository _reviewContextRepository;

        /// <summary>Initialises a new context builder.</summary>
        public IssueValidationContextBuilder(
            IRepositoryRepository repositoryRepository,
            IRepoFileRepository fileRepository,
            IReviewTypeRepository reviewTypeRepository,
            IReviewContextRepository reviewContextRepository)
        {
            _repositoryRepository = repositoryRepository;
            _fileRepository = fileRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _reviewContextRepository = reviewContextRepository;
        }

        /// <summary>Builds the validation prompts, reusing the original detection context where available.</summary>
        public async Task<IssueValidationContext> BuildAsync(Finding finding, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(finding);

            Repository repository = await _repositoryRepository.GetByIdAsync(finding.RepositoryId, cancellationToken).ConfigureAwait(false);
            RepoFile file = finding.RepoFileId.HasValue
                ? await _fileRepository.GetByIdAsync(finding.RepoFileId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            ReviewType reviewType = finding.ReviewTypeId.HasValue
                ? await _reviewTypeRepository.GetByIdAsync(finding.ReviewTypeId.Value, cancellationToken).ConfigureAwait(false)
                : null;
            AIReviewContext original = finding.ReviewContextId.HasValue
                ? await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            SourceSelection source = await SelectSourceAsync(repository, file, original, cancellationToken).ConfigureAwait(false);
            IssueValidationContextReference reference = BuildReference(original, file, source);

            IssueValidationContext context = new()
            {
                SystemPrompt = IssueValidationPrompts.SystemPrompt,
                PromptVersion = IssueValidationPrompts.Version,
                Reference = reference,
                UserPrompt = BuildUserPrompt(finding, repository, reviewType, original, source, reference)
            };

            if (source.Mode is not IssueValidationContextMode.Unavailable)
            {
                context.SuppliedSources[source.Path] = CountLines(source.Content);
            }

            return context;
        }

        private static async Task<SourceSelection> SelectSourceAsync(
            Repository repository,
            RepoFile file,
            AIReviewContext original,
            CancellationToken cancellationToken)
        {
            if (original is not null)
            {
                return new SourceSelection
                {
                    Mode = IssueValidationContextMode.OriginalSnapshot,
                    Path = string.IsNullOrWhiteSpace(original.FilePath) ? file?.RelativePath ?? string.Empty : original.FilePath,
                    Content = original.FileContent ?? string.Empty,
                    Sha256 = original.Sha256 ?? string.Empty
                };
            }

            string current = await TryReadCurrentSourceAsync(repository, file, cancellationToken).ConfigureAwait(false);

            if (current is null)
            {
                return new SourceSelection
                {
                    Mode = IssueValidationContextMode.Unavailable,
                    Path = file?.RelativePath ?? string.Empty
                };
            }

            return new SourceSelection
            {
                Mode = IssueValidationContextMode.CurrentSource,
                Path = file.RelativePath,
                Content = current,
                Sha256 = file.Sha256 ?? string.Empty
            };
        }

        private static async Task<string> TryReadCurrentSourceAsync(Repository repository, RepoFile file, CancellationToken cancellationToken)
        {
            if (repository is null || file is null || file.IsRemoved || string.IsNullOrWhiteSpace(repository.LocalPath))
            {
                return null;
            }

            try
            {
                string root = Path.GetFullPath(repository.LocalPath);
                string relative = file.RelativePath.Replace(Consts.CharForwardSlash, Path.DirectorySeparatorChar);
                string full = Path.GetFullPath(Path.Combine(root, relative));

                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(full, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static IssueValidationContextReference BuildReference(AIReviewContext original, RepoFile file, SourceSelection source)
        {
            IssueValidationContextReference reference = new()
            {
                Mode = source.Mode,
                ReviewContextId = original?.Id,
                FilePath = source.Path,
                SuppliedSha256 = source.Sha256,
                OriginalSha256 = original?.Sha256 ?? string.Empty,
                ContextItemCount = original?.ContextItems.Count ?? 0,
                TruncatedContextItemCount = original?.ContextItems.Count(item => item.Truncated) ?? 0,
                RequestedContextKeys = [.. original?.RequestedContextKeys ?? []]
            };

            if (original is not null)
            {
                reference.SourceChangedSinceDetection = file is null
                    || file.IsRemoved
                    || !string.Equals(file.Sha256, original.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            AddNotes(reference, original, file);
            return reference;
        }

        private static void AddNotes(IssueValidationContextReference reference, AIReviewContext original, RepoFile file)
        {
            switch (reference.Mode)
            {
                case IssueValidationContextMode.CurrentSource:
                    reference.Notes.Add(Strings.ValidationContextNoteLegacy);
                    break;

                case IssueValidationContextMode.Unavailable:
                    reference.Notes.Add(Strings.ValidationContextNoteUnavailable);
                    break;

                case IssueValidationContextMode.OriginalSnapshot when file is null || file.IsRemoved:
                    reference.Notes.Add(Strings.ValidationContextNoteFileRemoved);
                    break;

                case IssueValidationContextMode.OriginalSnapshot when reference.SourceChangedSinceDetection:
                    reference.Notes.Add(Strings.ValidationContextNoteSourceChanged);
                    break;
            }

            if (original is not null && IsDetectionMetadataMissing(original))
            {
                reference.Notes.Add(Strings.ValidationContextNoteDetectionMetadataMissing);
            }

            if (reference.TruncatedContextItemCount > 0)
            {
                reference.Notes.Add(string.Format(Strings.ValidationContextNoteTruncated, reference.TruncatedContextItemCount));
            }

            reference.Notes.Add(Strings.ValidationContextNoteNoLineReferences);
        }

        private static bool IsDetectionMetadataMissing(AIReviewContext original)
        {
            return string.IsNullOrWhiteSpace(original.EngineModel) && string.IsNullOrWhiteSpace(original.ReviewPrompt);
        }

        private static string BuildUserPrompt(
            Finding finding,
            Repository repository,
            ReviewType reviewType,
            AIReviewContext original,
            SourceSelection source,
            IssueValidationContextReference reference)
        {
            StringBuilder builder = new();

            AppendFinding(builder, finding, repository, reviewType, source);
            AppendDetectionMetadata(builder, original, reference);
            AppendSource(builder, source);
            AppendAdditionalContext(builder, original);
            AppendNotes(builder, reference);

            return builder.ToString();
        }

        private static void AppendFinding(StringBuilder builder, Finding finding, Repository repository, ReviewType reviewType, SourceSelection source)
        {
            builder.AppendLine(FindingHeading);
            builder.AppendLine($"Finding id: {finding.Id}");
            builder.AppendLine($"Repository: {repository?.Name ?? Unknown}");
            builder.AppendLine($"Review type: {reviewType?.Name ?? Unknown}");
            builder.AppendLine($"Severity: {finding.Severity}");
            builder.AppendLine($"Title: {finding.Title}");
            builder.AppendLine($"File: {(string.IsNullOrWhiteSpace(source.Path) ? NotRecorded : source.Path)}");
            builder.AppendLine(LineReferencesLine);
            builder.AppendLine(DescriptionLine);
            builder.AppendLine(finding.Description);
            builder.AppendLine(RecommendationLine);
            builder.AppendLine(string.IsNullOrWhiteSpace(finding.SuggestedFix) ? None : finding.SuggestedFix);
            builder.AppendLine(EvidenceNotRecordedLine);
            builder.AppendLine();
        }

        private static void AppendDetectionMetadata(StringBuilder builder, AIReviewContext original, IssueValidationContextReference reference)
        {
            builder.AppendLine(DetectionHeading);

            if (original is null)
            {
                builder.AppendLine(DetectionContextNotStoredLine);
                builder.AppendLine();
                return;
            }

            builder.AppendLine($"Detection engine provider: {ValueOrNotRecorded(original.EngineProvider)}");
            builder.AppendLine($"Detection engine model: {ValueOrNotRecorded(original.EngineModel)}");
            builder.AppendLine($"Detection engine name: {ValueOrNotRecorded(original.EngineName)}");
            builder.AppendLine($"Review definition version: {ValueOrNotRecorded(original.ReviewDefinitionVersion)}");
            builder.AppendLine($"Detection performed (UTC): {original.CreatedUtc:O}");
            builder.AppendLine($"Context requested by the detection engine: {FormatRequestedKeys(original, reference)}");
            builder.AppendLine($"Context actually supplied to the detection engine: the file content below and {reference.ContextItemCount} additional context item(s)");
            builder.AppendLine(DetectionInstructionsLine);
            AppendDelimited(builder, original.ReviewInstructions);
            builder.AppendLine(ReviewPromptLine);
            AppendDelimited(builder, original.ReviewPrompt);
            builder.AppendLine();
        }

        private static string FormatRequestedKeys(AIReviewContext original, IssueValidationContextReference reference)
        {
            if (reference.RequestedContextKeys.Count > 0)
            {
                return string.Join(Consts.ListSeparator, reference.RequestedContextKeys);
            }

            return IsDetectionMetadataMissing(original) ? NotRecorded : None;
        }

        private static void AppendSource(StringBuilder builder, SourceSelection source)
        {
            builder.AppendLine(SourceHeading);

            if (source.Mode is IssueValidationContextMode.Unavailable)
            {
                builder.AppendLine(NoSourceLine);
                builder.AppendLine();
                return;
            }

            string origin = source.Mode is IssueValidationContextMode.OriginalSnapshot
                ? OriginalSnapshotOrigin
                : CurrentSourceOrigin;

            builder.AppendLine($"File: {source.Path} ({origin})");
            builder.AppendLine(NumberLines(source.Content));
            builder.AppendLine();
        }

        private static void AppendAdditionalContext(StringBuilder builder, AIReviewContext original)
        {
            if (original is null || original.ContextItems.Count == 0)
            {
                return;
            }

            builder.AppendLine(AdditionalContextHeading);

            foreach (ReviewContextItem item in original.ContextItems)
            {
                string truncation = item.Truncated
                    ? $" [TRUNCATED{(item.OriginalLength > 0 ? $" from {item.OriginalLength} characters" : string.Empty)}]"
                    : string.Empty;
                string sourcePath = string.IsNullOrWhiteSpace(item.SourcePath) ? string.Empty : $" ({item.SourcePath})";

                builder.AppendLine($"## {(string.IsNullOrWhiteSpace(item.Title) ? Unknown : item.Title)}{sourcePath}{truncation}");
                builder.AppendLine(item.Content);
            }

            builder.AppendLine();
        }

        private static void AppendNotes(StringBuilder builder, IssueValidationContextReference reference)
        {
            if (reference.Notes.Count == 0)
            {
                return;
            }

            builder.AppendLine(NotesHeading);

            foreach (string note in reference.Notes)
            {
                builder.AppendLine(string.Concat(ListItemPrefix, note));
            }
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

        private static string[] SplitLines(string content)
        {
            return (content ?? string.Empty).Replace(LineEndingCarriageReturn, string.Empty).Split(Consts.CharNewLine);
        }

        private static int CountLines(string content)
        {
            return SplitLines(content).Length;
        }

        private static string NumberLines(string content)
        {
            string[] lines = SplitLines(content);
            StringBuilder numbered = new();

            for (int index = 0; index < lines.Length; index++)
            {
                numbered.Append(index + 1);
                numbered.Append(LineNumberSeparator);
                numbered.AppendLine(lines[index]);
            }

            return numbered.ToString();
        }

        private sealed class SourceSelection
        {
            public IssueValidationContextMode Mode { get; init; }

            public string Path { get; init; } = string.Empty;

            public string Content { get; init; } = string.Empty;

            public string Sha256 { get; init; } = string.Empty;
        }
    }
}
