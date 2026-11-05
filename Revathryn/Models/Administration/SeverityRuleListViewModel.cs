using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>List of configured severity override rules for the administration page.</summary>
    public sealed class SeverityRuleListViewModel
    {
        /// <summary>Configured severity override rules in evaluation order.</summary>
        public IReadOnlyList<SeverityRule> Rules { get; set; } = [];
    }
}
