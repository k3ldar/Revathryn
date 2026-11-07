using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Provides the cached descriptive structure summary used to prime reviews.</summary>
    public interface IArchitectureSummaryService
    {
        /// <summary>
        /// Returns the cached summary for the supplied file, or generates and caches one when missing or stale.
        /// Returns an empty string when the pass is disabled, skipped, or failed.
        /// </summary>
        Task<string> GetOrCreateAsync(RepoFile file, string fileContent, string language, CancellationToken cancellationToken = default);
    }
}
