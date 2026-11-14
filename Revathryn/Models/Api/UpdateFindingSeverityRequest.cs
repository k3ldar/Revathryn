using Revathryn.Contracts.Enums;

namespace Revathryn.Models.Api
{
    /// <summary>Request to change the severity of a single finding.</summary>
    public sealed class UpdateFindingSeverityRequest
    {
        /// <summary>The new severity to apply.</summary>
        public FindingSeverity Severity { get; set; }
    }
}
