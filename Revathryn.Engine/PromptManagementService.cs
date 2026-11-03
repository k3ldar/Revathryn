using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Review;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Manages prompt version lifecycles: drafts, commits, independent activation and rollback. Activation
    /// always requires an explicit caller action and existing versions are never overwritten.
    /// </summary>
    public sealed class PromptManagementService : IPromptManagementService
    {
        private readonly IPromptRepository _promptRepository;
        private readonly IPromptVersionRepository _versionRepository;
        private readonly IPromptOptimisationRepository _optimisationRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public PromptManagementService(
            IPromptRepository promptRepository,
            IPromptVersionRepository versionRepository,
            IPromptOptimisationRepository optimisationRepository,
            IReviewTypeRepository reviewTypeRepository,
            IOperationalDiagnostics diagnostics)
        {
            _promptRepository = promptRepository;
            _versionRepository = versionRepository;
            _optimisationRepository = optimisationRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _diagnostics = diagnostics;
        }

        /// <summary>Returns the prompt management overview rows.</summary>
        public async Task<IReadOnlyList<PromptOverviewItem>> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Prompt> prompts = await _promptRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            List<PromptOverviewItem> items = [];

            foreach (Prompt prompt in prompts)
            {
                PromptVersion active = await _versionRepository.GetActiveAsync(prompt.Id, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<PromptVersion> versions = await _versionRepository.GetByPromptAsync(prompt.Id, cancellationToken).ConfigureAwait(false);

                string reviewTypeName = string.Empty;

                if (prompt.ReviewTypeId.HasValue)
                {
                    ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(prompt.ReviewTypeId.Value, cancellationToken).ConfigureAwait(false);
                    reviewTypeName = reviewType?.Name ?? string.Empty;
                }

                items.Add(
                    new PromptOverviewItem
                    {
                        PromptId = prompt.Id,
                        PromptType = prompt.PromptType,
                        ReviewTypeId = prompt.ReviewTypeId,
                        ReviewTypeName = reviewTypeName,
                        ActiveVersionNumber = active?.VersionNumber,
                        ActiveVersionId = active?.Id,
                        LastModifiedUtc = prompt.LastModifiedUtc,
                        VersionCount = versions.Count
                    });
            }

            return items;
        }

        /// <summary>Returns a prompt identity by id, or null when not found.</summary>
        public Task<Prompt> GetPromptAsync(long promptId, CancellationToken cancellationToken = default)
        {
            return _promptRepository.GetByIdAsync(promptId, cancellationToken);
        }

        /// <summary>Returns all versions for a prompt identity, newest first.</summary>
        public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(long promptId, CancellationToken cancellationToken = default)
        {
            return _versionRepository.GetByPromptAsync(promptId, cancellationToken);
        }

        /// <summary>Returns a prompt version by id, or null when not found.</summary>
        public Task<PromptVersion> GetVersionAsync(long versionId, CancellationToken cancellationToken = default)
        {
            return _versionRepository.GetByIdAsync(versionId, cancellationToken);
        }

        /// <summary>Creates a new editable draft version for a prompt identity. Returns null when the prompt is missing.</summary>
        public async Task<PromptVersion> CreateDraftAsync(
            long promptId,
            string promptText,
            string description,
            PromptOrigin origin,
            long? sourceOptimisationId,
            string createdBy,
            CancellationToken cancellationToken = default)
        {
            Prompt prompt = await _promptRepository.GetByIdAsync(promptId, cancellationToken).ConfigureAwait(false);

            if (prompt is null)
            {
                return null;
            }

            DateTime utcNow = DateTime.UtcNow;
            int maxVersion = await _versionRepository.GetMaxVersionNumberAsync(promptId, cancellationToken).ConfigureAwait(false);

            PromptVersion draft = new()
            {
                PromptId = promptId,
                VersionNumber = maxVersion + 1,
                PromptText = Normalise(promptText),
                Description = Normalise(description),
                Origin = origin,
                SourceOptimisationId = sourceOptimisationId,
                CreatedUtc = utcNow,
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? Consts.SystemActor : createdBy,
                IsActive = false,
                IsDraft = true,
                ActivatedUtc = null
            };

            long id = await _versionRepository.AddAsync(draft, cancellationToken).ConfigureAwait(false);
            draft.Id = id;

            await _promptRepository.TouchAsync(promptId, utcNow, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptManagementServiceComponent,
                Consts.CreatePromptDraftOperation,
                string.Format(Strings.PromptDraftCreatedLog, id, promptId, draft.CreatedBy));

            return draft;
        }

        /// <summary>Updates the text and description of an existing draft. Returns null when the version is not an editable draft.</summary>
        public async Task<PromptVersion> UpdateDraftAsync(long versionId, string promptText, string description, CancellationToken cancellationToken = default)
        {
            bool updated = await _versionRepository.UpdateDraftAsync(versionId, Normalise(promptText), Normalise(description), DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            if (!updated)
            {
                return null;
            }

            PromptVersion version = await _versionRepository.GetByIdAsync(versionId, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptManagementServiceComponent,
                Consts.CreatePromptDraftOperation,
                string.Format(Strings.PromptDraftUpdatedLog, versionId));

            return version;
        }

        /// <summary>Commits a draft as an immutable historical version without activating it.</summary>
        public async Task<PromptVersion> CommitDraftAsync(long versionId, CancellationToken cancellationToken = default)
        {
            PromptVersion committed = await _versionRepository.CommitDraftAsync(versionId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            if (committed is null)
            {
                return null;
            }

            await _promptRepository.TouchAsync(committed.PromptId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptManagementServiceComponent,
                Consts.ActivatePromptOperation,
                string.Format(Strings.PromptDraftCommittedLog, versionId, committed.VersionNumber));

            return committed;
        }

        /// <summary>Explicitly activates a version and deactivates all others of the same prompt identity.</summary>
        public async Task<PromptVersion> ActivateAsync(long versionId, string activatedBy, CancellationToken cancellationToken = default)
        {
            PromptVersion activated = await _versionRepository.ActivateAsync(versionId, activatedBy, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            if (activated is null)
            {
                return null;
            }

            await _promptRepository.TouchAsync(activated.PromptId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptManagementServiceComponent,
                Consts.ActivatePromptOperation,
                string.Format(Strings.PromptVersionActivatedLog, versionId, activatedBy ?? string.Empty));

            return activated;
        }

        /// <summary>Returns the system prompt recommendation produced by an optimisation, or null.</summary>
        public async Task<PromptRecommendation> GetSystemRecommendationAsync(long optimisationId, CancellationToken cancellationToken = default)
        {
            PromptOptimisation optimisation = await _optimisationRepository.GetByIdAsync(optimisationId, cancellationToken).ConfigureAwait(false);

            if (optimisation is null || string.IsNullOrWhiteSpace(optimisation.SystemPromptRecommendationJson))
            {
                return null;
            }

            return PromptOptimisationJson.ReadRecommendation(optimisation.SystemPromptRecommendationJson);
        }

        /// <summary>Returns the review prompt recommendation produced by an optimisation, or null.</summary>
        public async Task<PromptRecommendation> GetReviewRecommendationAsync(long optimisationId, CancellationToken cancellationToken = default)
        {
            PromptOptimisation optimisation = await _optimisationRepository.GetByIdAsync(optimisationId, cancellationToken).ConfigureAwait(false);

            if (optimisation is null || string.IsNullOrWhiteSpace(optimisation.ReviewPromptRecommendationJson))
            {
                return null;
            }

            return PromptOptimisationJson.ReadRecommendation(optimisation.ReviewPromptRecommendationJson);
        }

        private static string Normalise(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }
}
