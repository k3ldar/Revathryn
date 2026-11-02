using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Tests
{
    /// <summary>Configurable in-memory issue validation service for controller tests.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class FakeIssueValidationService : IIssueValidationService
    {
        public List<IssueValidation> Validations { get; } = [];

        public IssueValidationRequestResult RequestResult { get; set; } = new();

        public IssueValidationRequestResult RetryResult { get; set; } = new();

        public long LastRequestedFindingId { get; private set; }

        public long LastRetriedValidationId { get; private set; }

        public int RequestCallCount { get; private set; }

        public int RetryCallCount { get; private set; }

        public Task<IssueValidationRequestResult> RequestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            RequestCallCount++;
            LastRequestedFindingId = findingId;
            return Task.FromResult(RequestResult);
        }

        public Task<IssueValidationRequestResult> RetryAsync(long validationId, CancellationToken cancellationToken = default)
        {
            RetryCallCount++;
            LastRetriedValidationId = validationId;
            return Task.FromResult(RetryResult);
        }

        public Task<IssueValidation> GetByIdAsync(long validationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Validations.FirstOrDefault(v => v.Id == validationId));
        }

        public Task<IReadOnlyList<IssueValidation>> GetByFindingAsync(long findingId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IssueValidation> result = [.. Validations.Where(v => v.FindingId == findingId).OrderByDescending(v => v.Id)];
            return Task.FromResult(result);
        }

        public Task<IssueValidation> GetLatestAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Validations.Where(v => v.FindingId == findingId).OrderByDescending(v => v.Id).FirstOrDefault());
        }

        public Task<IssueValidation> GetActiveAsync(long findingId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Validations.FirstOrDefault(v => v.FindingId == findingId
                && v.Status is Contracts.Enums.IssueValidationStatus.Pending or Contracts.Enums.IssueValidationStatus.Running));
        }

        public Task<IReadOnlyDictionary<long, IssueValidation>> GetLatestCompletedAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken = default)
        {
            Dictionary<long, IssueValidation> result = Validations
                .Where(v => findingIds.Contains(v.FindingId) && v.Status is Contracts.Enums.IssueValidationStatus.Completed)
                .GroupBy(v => v.FindingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Id).First());

            return Task.FromResult<IReadOnlyDictionary<long, IssueValidation>>(result);
        }
    }
}
