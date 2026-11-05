using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Services
{
    /// <summary>Applies configured severity override rules to AI-supplied severities.</summary>
    public interface ISeverityRuleEngine
    {
        /// <summary>
        /// Returns the effective severity after applying the first matching rule, or the
        /// original severity when no rule matches.
        /// </summary>
        FindingSeverity Apply(FindingSeverity originalSeverity, string title, string description);
    }
}
