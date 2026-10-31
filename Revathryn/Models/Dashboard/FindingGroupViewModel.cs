using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>A group of findings sharing a severity and review type.</summary>
    public sealed class FindingGroupViewModel
    {
        /// <summary>Severity shared by the group.</summary>
        public FindingSeverity Severity { get; set; }

        /// <summary>Display name of the review type that produced the findings.</summary>
        public string ReviewTypeName { get; set; } = string.Empty;

        /// <summary>Findings in the group.</summary>
        public IReadOnlyList<Finding> Findings { get; set; } = [];
    }
}
