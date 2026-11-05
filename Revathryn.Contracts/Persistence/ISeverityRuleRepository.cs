using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Data access for severity override rules.</summary>
    public interface ISeverityRuleRepository
    {
        /// <summary>Returns the rule with the given id, or null when not found.</summary>
        Task<SeverityRule> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Returns all rules in evaluation order.</summary>
        Task<IReadOnlyList<SeverityRule>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns all enabled rules in evaluation order.</summary>
        Task<IReadOnlyList<SeverityRule>> GetEnabledOrderedAsync(CancellationToken cancellationToken = default);

        /// <summary>Inserts the rule and returns its assigned id.</summary>
        Task<long> AddAsync(SeverityRule rule, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing rule.</summary>
        Task UpdateAsync(SeverityRule rule, CancellationToken cancellationToken = default);

        /// <summary>Deletes the rule with the given id.</summary>
        Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    }
}
