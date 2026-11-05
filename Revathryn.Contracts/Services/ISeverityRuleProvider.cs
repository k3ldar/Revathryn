using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Provides the currently active severity override rules to runtime services.</summary>
    public interface ISeverityRuleProvider
    {
        /// <summary>The currently active enabled rules in evaluation order.</summary>
        IReadOnlyList<SeverityRule> Rules { get; }

        /// <summary>Replaces the active rule snapshot with the supplied persisted rules.</summary>
        void Refresh(IReadOnlyList<SeverityRule> rules);
    }
}
