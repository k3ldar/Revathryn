namespace Revathryn.Contracts.Enums
{
    /// <summary>Severity of a finding produced by a review.</summary>
    public enum FindingSeverity
    {
        /// <summary>Informational observation that requires no action.</summary>
        Info = 0,

        /// <summary>A minor improvement with limited impact.</summary>
        Low = 1,

        /// <summary>A notable issue that should be addressed.</summary>
        Medium = 2,

        /// <summary>A serious issue that likely requires correction.</summary>
        High = 3,

        /// <summary>A critical issue that must be addressed.</summary>
        Critical = 4
    }
}
