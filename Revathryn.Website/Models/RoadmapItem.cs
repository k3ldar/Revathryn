namespace Revathryn.Website.Models
{
    /// <summary>Represents a single entry on the project roadmap.</summary>
    public sealed class RoadmapItem
    {
        /// <summary>Gets or sets the item title.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the item description.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the item status.</summary>
        public RoadmapStatus Status { get; set; }

        /// <summary>Gets or sets an optional target milestone.</summary>
        public string Milestone { get; set; }
    }
}
