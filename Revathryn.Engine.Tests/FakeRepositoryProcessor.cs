using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine.Tests
{
    internal sealed class FakeRepositoryProcessor : IRepositoryProcessor
    {
        public List<long> ProcessedIds { get; } = [];

        public Task<RepositoryScanSummary> ProcessAsync(long repositoryId, CancellationToken cancellationToken, IProgress<RepositoryScanProgress> progress = null, bool forceFullScan = false)
        {
            ProcessedIds.Add(repositoryId);
            return Task.FromResult(new RepositoryScanSummary { RepositoryId = repositoryId });
        }

        public Task<IReadOnlyList<RepositoryScanSummary>> ProcessAllEnabledAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RepositoryScanSummary>>([]);
        }
    }
}
