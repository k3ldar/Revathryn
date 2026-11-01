using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>Read-side queries for issues identified by Revathryn.</summary>
    public sealed class IssueQueryService : IIssueQueryService
    {
        private readonly IFindingRepository _findingRepository;
        private readonly IRepositoryRepository _repositoryRepository;

        /// <summary>Initialises a new issue query service.</summary>
        public IssueQueryService(IFindingRepository findingRepository, IRepositoryRepository repositoryRepository)
        {
            _findingRepository = findingRepository;
            _repositoryRepository = repositoryRepository;
        }

        /// <summary>Returns a filtered, sorted and paged set of findings.</summary>
        public Task<FindingQueryResult> QueryAsync(FindingQuery query, CancellationToken cancellationToken = default)
        {
            return _findingRepository.QueryAsync(query, cancellationToken);
        }

        /// <summary>Returns aggregated outstanding issue counts and recent activity.</summary>
        public async Task<IssueSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Finding> outstanding = await _findingRepository.GetOutstandingAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<Repository> repositories = await _repositoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            Dictionary<long, string> names = [];
            foreach (Repository repository in repositories)
            {
                names[repository.Id] = repository.Name;
            }

            return new IssueSummary
            {
                TotalCount = outstanding.Count,
                BySeverity = BuildSeverityCounts(outstanding),
                ByRepository = BuildRepositoryCounts(outstanding, names),
                RecentlyIdentified = [.. outstanding.OrderByDescending(finding => finding.CreatedDate).Take(DefaultRecentIssueCount)],
                RecentlyUpdated = [.. outstanding.OrderByDescending(finding => finding.LastModifiedDate).Take(DefaultRecentIssueCount)]
            };
        }

        private static IReadOnlyList<IssueSeverityCount> BuildSeverityCounts(IReadOnlyList<Finding> findings)
        {
            List<IssueSeverityCount> counts = [];
            foreach (FindingSeverity severity in Enum.GetValues<FindingSeverity>())
            {
                int count = findings.Count(finding => finding.Severity == severity);
                counts.Add(new IssueSeverityCount
                {
                    Severity = severity,
                    Count = count
                });
            }

            return counts;
        }

        private static IReadOnlyList<IssueRepositoryCount> BuildRepositoryCounts(IReadOnlyList<Finding> findings, Dictionary<long, string> names)
        {
            List<IssueRepositoryCount> counts = [];
            foreach (IGrouping<long, Finding> group in findings.GroupBy(finding => finding.RepositoryId).OrderByDescending(group => group.Count()))
            {
                names.TryGetValue(group.Key, out string name);
                counts.Add(new IssueRepositoryCount
                {
                    RepositoryId = group.Key,
                    RepositoryName = name ?? group.Key.ToString(),
                    Count = group.Count()
                });
            }

            return counts;
        }
    }
}
