using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>Number of outstanding issues for a single severity.</summary>
    public sealed class IssueSeverityCount
    {
        /// <summary>The issue severity.</summary>
        public FindingSeverity Severity { get; set; }

        /// <summary>Number of outstanding issues with the severity.</summary>
        public int Count { get; set; }
    }
}
