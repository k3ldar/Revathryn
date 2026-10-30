namespace Revathryn.Contracts.Enums
{
    /// <summary>Lifecycle status of a finding.</summary>
    public enum FindingStatus
    {
        /// <summary>Newly created and not yet triaged.</summary>
        New = 0,

        /// <summary>Reviewed by a human but not yet resolved.</summary>
        Reviewed = 1,

        /// <summary>Confirmed to be a false positive.</summary>
        FalsePositive = 2,

        /// <summary>Addressed in the source code.</summary>
        Resolved = 3,

        /// <summary>Deferred for later consideration.</summary>
        Deferred = 4
    }
}
