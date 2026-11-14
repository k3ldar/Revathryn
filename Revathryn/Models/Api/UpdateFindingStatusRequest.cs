using Revathryn.Contracts.Enums;

namespace Revathryn.Models.Api
{
    /// <summary>Request to change the status of a single finding.</summary>
    public sealed class UpdateFindingStatusRequest
    {
        /// <summary>The new status to apply.</summary>
        public FindingStatus Status { get; set; }
    }
}
