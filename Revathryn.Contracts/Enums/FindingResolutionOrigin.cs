namespace Revathryn.Contracts.Enums
{
    /// <summary>Describes how a finding reached a resolved state.</summary>
    public enum FindingResolutionOrigin
    {
        /// <summary>Not resolved.</summary>
        None = 0,

        /// <summary>Resolved by a human action.</summary>
        Manual = 1,

        /// <summary>Resolved automatically by a subsequent review that no longer reported the issue.</summary>
        Auto = 2
    }
}
