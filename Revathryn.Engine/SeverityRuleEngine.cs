using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>
    /// Applies ordered, first-match-wins severity override rules to an AI-supplied severity.
    /// Matching is case-insensitive substring matching against the configured finding fields.
    /// </summary>
    public sealed class SeverityRuleEngine : ISeverityRuleEngine
    {
        private readonly ISeverityRuleProvider _ruleProvider;

        /// <summary>Initialises a new rule engine.</summary>
        public SeverityRuleEngine(ISeverityRuleProvider ruleProvider)
        {
            _ruleProvider = ruleProvider;
        }

        /// <summary>
        /// Returns the effective severity after applying the first matching rule, or the
        /// original severity when no rule matches.
        /// </summary>
        public FindingSeverity Apply(FindingSeverity originalSeverity, string title, string description)
        {
            string safeTitle = title ?? string.Empty;
            string safeDescription = description ?? string.Empty;

            foreach (SeverityRule rule in _ruleProvider.Rules)
            {
                if (!Matches(rule, originalSeverity, safeTitle, safeDescription))
                {
                    continue;
                }

                return ApplyAction(rule.Action, originalSeverity, rule.TargetSeverity);
            }

            return originalSeverity;
        }

        private static bool Matches(SeverityRule rule, FindingSeverity severity, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(rule.MatchText))
            {
                return false;
            }

            if (rule.MatchSeverity.HasValue && rule.MatchSeverity.Value != severity)
            {
                return false;
            }

            bool matchesTitle = (rule.MatchField is SeverityRuleMatchField.Title or SeverityRuleMatchField.Both)
                && title.Contains(rule.MatchText, StringComparison.OrdinalIgnoreCase);

            bool matchesDescription = (rule.MatchField is SeverityRuleMatchField.Description or SeverityRuleMatchField.Both)
                && description.Contains(rule.MatchText, StringComparison.OrdinalIgnoreCase);

            return matchesTitle || matchesDescription;
        }

        private static FindingSeverity ApplyAction(SeverityRuleAction action, FindingSeverity originalSeverity, FindingSeverity targetSeverity)
        {
            if (action is SeverityRuleAction.Cap)
            {
                return (FindingSeverity)Math.Min((int)originalSeverity, (int)targetSeverity);
            }

            return targetSeverity;
        }
    }
}
