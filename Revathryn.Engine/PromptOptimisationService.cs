using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>
    /// Requests and queries guided prompt optimisation analyses initiated from a completed issue validation.
    /// The review engine investigates the disagreement and recommends prompt improvements; it never activates them.
    /// </summary>
    public sealed class PromptOptimisationService : IPromptOptimisationService
    {
        private readonly IFindingRepository _findingRepository;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IIssueValidationRepository _validationRepository;
        private readonly IReviewContextRepository _reviewContextRepository;
        private readonly IPromptOptimisationRepository _optimisationRepository;
        private readonly IReviewEngineConfigurationService _configurationService;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public PromptOptimisationService(
            IFindingRepository findingRepository,
            IRepositoryRepository repositoryRepository,
            IIssueValidationRepository validationRepository,
            IReviewContextRepository reviewContextRepository,
            IPromptOptimisationRepository optimisationRepository,
            IReviewEngineConfigurationService configurationService,
            IOperationalDiagnostics diagnostics)
        {
            _findingRepository = findingRepository;
            _repositoryRepository = repositoryRepository;
            _validationRepository = validationRepository;
            _reviewContextRepository = reviewContextRepository;
            _optimisationRepository = optimisationRepository;
            _configurationService = configurationService;
            _diagnostics = diagnostics;
        }

        /// <summary>Queues a prompt optimisation for the finding, or returns the one already in progress.</summary>
        public Task<PromptOptimisationStartResult> RequestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return QueueAsync(findingId, null, cancellationToken);
        }

        /// <summary>Queues a new optimisation that retries a failed one. The failed record is left intact.</summary>
        public async Task<PromptOptimisationStartResult> RetryAsync(long optimisationId, CancellationToken cancellationToken = default)
        {
            PromptOptimisation failed = await _optimisationRepository.GetByIdAsync(optimisationId, cancellationToken).ConfigureAwait(false);

            if (failed is null || failed.Status is not PromptOptimisationStatus.Failed)
            {
                return Rejected(PromptOptimisationRejection.NotRetryable, Strings.PromptOptimisationNotRetryable);
            }

            return await QueueAsync(failed.FindingId, failed.Id, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns an optimisation by id, or null when not found.</summary>
        public Task<PromptOptimisation> GetByIdAsync(long optimisationId, CancellationToken cancellationToken = default)
        {
            return _optimisationRepository.GetByIdAsync(optimisationId, cancellationToken);
        }

        /// <summary>Returns all optimisations for a finding, newest first.</summary>
        public Task<IReadOnlyList<PromptOptimisation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return _optimisationRepository.GetByFindingAsync(findingId, cancellationToken);
        }

        /// <summary>Returns the most recent completed optimisation for a finding, or null.</summary>
        public Task<PromptOptimisation> GetLatestCompletedAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return _optimisationRepository.GetLatestCompletedAsync(findingId, cancellationToken);
        }

        private async Task<PromptOptimisationStartResult> QueueAsync(long findingId, long? retryOfOptimisationId, CancellationToken cancellationToken)
        {
            Finding finding = await _findingRepository.GetByIdAsync(findingId, cancellationToken).ConfigureAwait(false);

            if (finding is null)
            {
                return Rejected(PromptOptimisationRejection.IssueNotFound, Strings.PromptOptimisationIssueNotFound);
            }

            Repository repository = await _repositoryRepository.GetByIdAsync(finding.RepositoryId, cancellationToken).ConfigureAwait(false);

            if (repository is null)
            {
                return Rejected(PromptOptimisationRejection.RepositoryNotFound, Strings.PromptOptimisationRepositoryNotFound);
            }

            IssueValidation validation = await _validationRepository.GetLatestAsync(findingId, cancellationToken).ConfigureAwait(false);

            if (validation is null || validation.Status is not IssueValidationStatus.Completed)
            {
                return Rejected(PromptOptimisationRejection.ValidationMissing, Strings.PromptOptimisationValidationMissing);
            }

            AIReviewContext original = finding.ReviewContextId.HasValue
                ? await _reviewContextRepository.GetByIdAsync(finding.ReviewContextId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            if (original is null)
            {
                return Rejected(PromptOptimisationRejection.ContextMissing, Strings.PromptOptimisationContextMissing);
            }

            PromptOptimisation active = await _optimisationRepository.GetActiveByValidationAsync(validation.Id, cancellationToken).ConfigureAwait(false);

            if (active is not null)
            {
                return AlreadyActive(active);
            }

            ReviewEngineAvailability availability = await _configurationService.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);

            if (!availability.IsAvailable)
            {
                return Rejected(PromptOptimisationRejection.ReviewEngineUnavailable, availability.Reason);
            }

            PromptOptimisation optimisation = new()
            {
                FindingId = finding.Id,
                RepositoryId = finding.RepositoryId,
                ReviewTypeId = finding.ReviewTypeId,
                IssueValidationId = validation.Id,
                Status = PromptOptimisationStatus.Pending,
                RetryOfOptimisationId = retryOfOptimisationId,
                CreatedUtc = DateTime.UtcNow,
                OriginalSystemPrompt = original.SystemPrompt,
                OriginalReviewPrompt = original.ReviewPrompt,
                OriginalSystemPromptVersionId = original.SystemPromptVersionId,
                OriginalReviewPromptVersionId = original.ReviewPromptVersionId
            };

            long id = await _optimisationRepository.TryAddAsync(optimisation, cancellationToken).ConfigureAwait(false);

            if (id == 0)
            {
                PromptOptimisation raced = await _optimisationRepository.GetActiveByValidationAsync(validation.Id, cancellationToken).ConfigureAwait(false);
                return raced is null
                    ? Rejected(PromptOptimisationRejection.ReviewEngineUnavailable, Strings.PromptOptimisationCouldNotBeQueued)
                    : AlreadyActive(raced);
            }

            optimisation.Id = id;

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.PromptOptimisationServiceComponent,
                Consts.QueueOptimisationOperation,
                string.Format(Strings.PromptOptimisationQueuedLog, id, findingId));

            return new PromptOptimisationStartResult
            {
                Optimisation = optimisation,
                Created = true,
                Message = Strings.PromptOptimisationQueued
            };
        }

        private static PromptOptimisationStartResult AlreadyActive(PromptOptimisation active)
        {
            return new PromptOptimisationStartResult
            {
                Optimisation = active,
                AlreadyActive = true,
                Message = Strings.PromptOptimisationAlreadyInProgress
            };
        }

        private static PromptOptimisationStartResult Rejected(PromptOptimisationRejection rejection, string message)
        {
            return new PromptOptimisationStartResult
            {
                Rejection = rejection,
                Message = message
            };
        }
    }
}
