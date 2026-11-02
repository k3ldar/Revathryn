namespace Revathryn.Contracts.Models
{
    /// <summary>Adjacent findings used to navigate between issue details while preserving listing ordering.</summary>
    public sealed class FindingNavigation
    {
        /// <summary>Id of the finding immediately preceding the current finding, or null when none.</summary>
        public long? PreviousId { get; set; }

        /// <summary>Id of the finding immediately following the current finding, or null when none.</summary>
        public long? NextId { get; set; }
    }
}
