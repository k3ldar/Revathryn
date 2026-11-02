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
    /// Requests and queries independent issue validations. Validation never modifies the finding it validates,
    /// and each request produces its own historical record.
    /// </summary>
    public sealed class IssueValidationService : IIssueValidationService
    {
        private readonly IFindingRepository _findingRepository;
        private readonly IRepositoryRepository _repositoryRepository;
        private readonly IIssueValidationRepository _validationRepository;
        private readonly IReviewEngineConfigurationService _configurationService;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new service.</summary>
        public IssueValidationService(
            IFindingRepository findingRepository,
            IRepositoryRepository repositoryRepository,
            IIssueValidationRepository validationRepository,
            IReviewEngineConfigurationService configurationService,
            IOperationalDiagnostics diagnostics)
        {
            _findingRepository = findingRepository;
            _repositoryRepository = repositoryRepository;
            _validationRepository = validationRepository;
            _configurationService = configurationService;
            _diagnostics = diagnostics;
        }

        /// <summary>Queues a validation, or returns the one already in progress.</summary>
        public Task<IssueValidationRequestResult> RequestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return QueueAsync(findingId, null, cancellationToken);
        }

        /// <summary>Queues a new validation that retries a failed one. The failed record is left intact.</summary>
        public async Task<IssueValidationRequestResult> RetryAsync(long validationId, CancellationToken cancellationToken = default)
        {
            IssueValidation failed = await _validationRepository.GetByIdAsync(validationId, cancellationToken).ConfigureAwait(false);

            if (failed is null || failed.Status is not IssueValidationStatus.Failed)
            {
                return Rejected(IssueValidationRejection.NotRetryable, Strings.ValidationNotRetryable);
            }

            return await QueueAsync(failed.FindingId, failed.Id, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns a validation by id, or null.</summary>
        public Task<IssueValidation> GetByIdAsync(long validationId, CancellationToken cancellationToken = default)
        {
            return _validationRepository.GetByIdAsync(validationId, cancellationToken);
        }

        /// <summary>Returns all validations for a finding, newest first.</summary>
        public Task<IReadOnlyList<IssueValidation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return _validationRepository.GetByFindingAsync(findingId, cancellationToken);
        }

        /// <summary>Returns the most recent validation for a finding, regardless of status, or null.</summary>
        public Task<IssueValidation> GetLatestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return _validationRepository.GetLatestAsync(findingId, cancellationToken);
        }

        /// <summary>Returns the pending or running validation for a finding, or null.</summary>
        public Task<IssueValidation> GetActiveAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return _validationRepository.GetActiveByFindingAsync(findingId, cancellationToken);
        }

        /// <summary>Returns the most recent completed validation for each of the supplied findings.</summary>
        public Task<IReadOnlyDictionary<long, IssueValidation>> GetLatestCompletedAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken = default)
        {
            return _validationRepository.GetLatestCompletedByFindingsAsync(findingIds, cancellationToken);
        }

        private async Task<IssueValidationRequestResult> QueueAsync(long findingId, long? retryOfValidationId, CancellationToken cancellationToken)
        {
            Finding finding = await _findingRepository.GetByIdAsync(findingId, cancellationToken).ConfigureAwait(false);

            if (finding is null)
            {
                return Rejected(IssueValidationRejection.IssueNotFound, Strings.ValidationIssueNotFound);
            }

            Repository repository = await _repositoryRepository.GetByIdAsync(finding.RepositoryId, cancellationToken).ConfigureAwait(false);

            if (repository is null)
            {
                return Rejected(IssueValidationRejection.RepositoryNotFound, Strings.ValidationRepositoryNotFound);
            }

            if (finding.Category is not FindingCategory.CodeIssue)
            {
                return Rejected(IssueValidationRejection.NotCodeIssue, Strings.ValidationNotCodeIssue);
            }

            IssueValidation active = await _validationRepository.GetActiveByFindingAsync(findingId, cancellationToken).ConfigureAwait(false);

            if (active is not null)
            {
                return AlreadyActive(active);
            }

            ReviewEngineAvailability availability = await _configurationService.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);

            if (!availability.IsAvailable)
            {
                return Rejected(IssueValidationRejection.ReviewEngineUnavailable, availability.Reason);
            }

            IssueValidation validation = new()
            {
                FindingId = finding.Id,
                RepositoryId = finding.RepositoryId,
                Status = IssueValidationStatus.Pending,
                RetryOfValidationId = retryOfValidationId,
                CreatedUtc = DateTime.UtcNow,
                OriginalSeverity = finding.Severity,
                OriginalTitle = finding.Title,
                OriginalDescription = finding.Description,
                OriginalSuggestedFix = finding.SuggestedFix
            };

            long id = await _validationRepository.TryAddAsync(validation, cancellationToken).ConfigureAwait(false);

            if (id == 0)
            {
                IssueValidation raced = await _validationRepository.GetActiveByFindingAsync(findingId, cancellationToken).ConfigureAwait(false);
                return raced is null
                    ? Rejected(IssueValidationRejection.ReviewEngineUnavailable, Strings.ValidationCouldNotBeQueued)
                    : AlreadyActive(raced);
            }

            validation.Id = id;

            _diagnostics.Log(
                OperationalLogLevel.Information,
                Consts.IssueValidationServiceComponent,
                Consts.QueueValidationOperation,
                string.Format(Strings.ValidationQueuedLog, id, findingId));

            return new IssueValidationRequestResult
            {
                Validation = validation,
                Created = true,
                Message = Strings.ValidationQueued
            };
        }

        private static IssueValidationRequestResult AlreadyActive(IssueValidation active)
        {
            return new IssueValidationRequestResult
            {
                Validation = active,
                AlreadyActive = true,
                Message = Strings.ValidationAlreadyInProgress
            };
        }

        private static IssueValidationRequestResult Rejected(IssueValidationRejection rejection, string message)
        {
            return new IssueValidationRequestResult
            {
                Rejection = rejection,
                Message = message
            };
        }
    }
}
