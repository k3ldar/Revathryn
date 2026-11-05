using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>
    /// Holds the current severity override rules so long-lived services observe
    /// administration-page changes without restarting.
    /// </summary>
    public sealed class SeverityRuleProvider : ISeverityRuleProvider
    {
        private IReadOnlyList<SeverityRule> _rules = [];

        /// <summary>The currently active enabled rules in evaluation order.</summary>
        public IReadOnlyList<SeverityRule> Rules => _rules;

        /// <summary>Replaces the active rule snapshot with the supplied persisted rules.</summary>
        public void Refresh(IReadOnlyList<SeverityRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            _rules = [.. rules
                .Where(rule => rule.Enabled)
                .OrderBy(rule => rule.Order)
                .ThenBy(rule => rule.Id)];
        }
    }
}
