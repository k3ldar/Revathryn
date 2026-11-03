using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>
    /// Resolves active prompts for detection and seeds initial prompt versions from existing configuration.
    /// System and review-specific prompts resolve independently so a missing version never blocks the other.
    /// </summary>
    public sealed class PromptResolutionService : IPromptResolutionService
    {
        private readonly IPromptRepository _promptRepository;
        private readonly IPromptVersionRepository _versionRepository;
        private readonly IAISettingsRepository _aiSettingsRepository;
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public PromptResolutionService(
            IPromptRepository promptRepository,
            IPromptVersionRepository versionRepository,
            IAISettingsRepository aiSettingsRepository,
            IReviewTypeRepository reviewTypeRepository,
            IRepositoryRepository repositoryRepository,
            IOperationalDiagnostics diagnostics)
        {
            _promptRepository = promptRepository;
            _versionRepository = versionRepository;
            _aiSettingsRepository = aiSettingsRepository;
            _reviewTypeRepository = reviewTypeRepository;
            _repositoryRepository = repositoryRepository;
            _diagnostics = diagnostics;
        }

        /// <summary>Resolves the active system and review-specific prompts for a review type.</summary>
        public async Task<ResolvedPrompts> ResolveForDetectionAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            PromptVersion systemVersion = await _versionRepository.GetActiveSystemAsync(cancellationToken).ConfigureAwait(false);
            PromptVersion reviewVersion = await _versionRepository.GetActiveByReviewTypeAsync(reviewTypeId, cancellationToken).ConfigureAwait(false);

            string systemPrompt = Normalise(systemVersion?.PromptText);
            string reviewPrompt = Normalise(reviewVersion?.PromptText);

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                AISettings settings = await _aiSettingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
                systemPrompt = Normalise(settings?.MasterPrompt);
            }

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                systemPrompt = DefaultReviewContext.DefaultMasterPrompt;
            }

            if (string.IsNullOrWhiteSpace(reviewPrompt))
            {
                ReviewType reviewType = await _reviewTypeRepository.GetByIdAsync(reviewTypeId, cancellationToken).ConfigureAwait(false);
                reviewPrompt = Normalise(reviewType?.Prompt);
            }

            return new ResolvedPrompts
            {
                SystemPrompt = systemPrompt ?? string.Empty,
                ReviewPrompt = reviewPrompt ?? string.Empty,
                SystemPromptVersionId = systemVersion?.Id,
                ReviewPromptVersionId = reviewVersion?.Id
            };
        }

        /// <summary>Returns the active system prompt version, or null when none is active.</summary>
        public Task<PromptVersion> GetActiveSystemVersionAsync(CancellationToken cancellationToken = default)
        {
            return _versionRepository.GetActiveSystemAsync(cancellationToken);
        }

        /// <summary>Returns the active review-specific prompt version, or null when none is active.</summary>
        public Task<PromptVersion> GetActiveReviewVersionAsync(long reviewTypeId, CancellationToken cancellationToken = default)
        {
            return _versionRepository.GetActiveByReviewTypeAsync(reviewTypeId, cancellationToken);
        }

        /// <summary>Seeds initial prompt versions from configuration. Existing identities are left untouched.</summary>
        public async Task SeedFromConfigurationAsync(CancellationToken cancellationToken = default)
        {
            DateTime utcNow = DateTime.UtcNow;

            AISettings settings = await _aiSettingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
            string systemPromptText = Normalise(settings?.MasterPrompt);

            if (string.IsNullOrWhiteSpace(systemPromptText))
            {
                systemPromptText = DefaultReviewContext.DefaultMasterPrompt;
            }

            Prompt systemPrompt = await EnsurePromptAsync(null, PromptType.System, utcNow, cancellationToken).ConfigureAwait(false);
            await EnsureInitialVersionAsync(systemPrompt, systemPromptText, utcNow, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<Repository> repositories = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            foreach (Repository repository in repositories)
            {
                IReadOnlyList<ReviewType> reviewTypes = await _reviewTypeRepository.GetByRepositoryAsync(repository.Id, cancellationToken).ConfigureAwait(false);

                foreach (ReviewType reviewType in reviewTypes)
                {
                    string reviewPromptText = Normalise(reviewType.Prompt);

                    if (string.IsNullOrWhiteSpace(reviewPromptText))
                    {
                        continue;
                    }

                    Prompt reviewPrompt = await EnsurePromptAsync(reviewType.Id, PromptType.Review, utcNow, cancellationToken).ConfigureAwait(false);
                    await EnsureInitialVersionAsync(reviewPrompt, reviewPromptText, utcNow, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<Prompt> EnsurePromptAsync(long? reviewTypeId, PromptType promptType, DateTime utcNow, CancellationToken cancellationToken)
        {
            Prompt prompt = promptType is PromptType.System
                ? await _promptRepository.GetSystemAsync(cancellationToken).ConfigureAwait(false)
                : await _promptRepository.GetByReviewTypeAsync(reviewTypeId.Value, cancellationToken).ConfigureAwait(false);

            if (prompt is not null)
            {
                return prompt;
            }

            long id = await _promptRepository.AddAsync(
                new Prompt
                {
                    PromptType = promptType,
                    ReviewTypeId = reviewTypeId,
                    CreatedUtc = utcNow,
                    LastModifiedUtc = utcNow
                },
                cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                PromptResolutionServiceComponent,
                ActivatePromptOperation,
                promptType is PromptType.System
                    ? PromptSeedSystemIdentityMessage
                    : string.Format(PromptSeedReviewIdentityMessage, reviewTypeId));

            return new Prompt
            {
                Id = id,
                PromptType = promptType,
                ReviewTypeId = reviewTypeId,
                CreatedUtc = utcNow,
                LastModifiedUtc = utcNow
            };
        }

        private async Task EnsureInitialVersionAsync(Prompt prompt, string promptText, DateTime utcNow, CancellationToken cancellationToken)
        {
            int maxVersion = await _versionRepository.GetMaxVersionNumberAsync(prompt.Id, cancellationToken).ConfigureAwait(false);

            if (maxVersion > 0)
            {
                return;
            }

            await _versionRepository.AddAsync(
                new PromptVersion
                {
                    PromptId = prompt.Id,
                    VersionNumber = PromptInitialVersionNumber,
                    PromptText = promptText,
                    Description = string.Empty,
                    Origin = PromptOrigin.Manual,
                    SourceOptimisationId = null,
                    CreatedUtc = utcNow,
                    CreatedBy = SystemActor,
                    IsActive = true,
                    IsDraft = false,
                    ActivatedUtc = utcNow
                },
                cancellationToken).ConfigureAwait(false);

            _diagnostics.Log(
                OperationalLogLevel.Information,
                PromptResolutionServiceComponent,
                ActivatePromptOperation,
                prompt.PromptType is PromptType.System
                    ? PromptSeedSystemVersionMessage
                    : string.Format(PromptSeedReviewVersionMessage, prompt.ReviewTypeId));
        }

        private static string Normalise(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }
}
