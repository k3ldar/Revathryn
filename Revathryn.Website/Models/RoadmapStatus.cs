namespace Revathryn.Website.Models
{
    /// <summary>Represents the current status of a roadmap item.</summary>
    public enum RoadmapStatus
    {
        /// <summary>The item is complete and available.</summary>
        Completed,

        /// <summary>The item is actively being worked on.</summary>
        InProgress,

        /// <summary>The item is planned but not yet started.</summary>
        Planned,

        /// <summary>The item is being considered for a future phase.</summary>
        UnderConsideration
    }
}
