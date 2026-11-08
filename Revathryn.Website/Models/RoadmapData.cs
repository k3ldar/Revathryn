namespace Revathryn.Website.Models
{
    /// <summary>The root model for the data-driven roadmap.</summary>
    public sealed class RoadmapData
    {
        /// <summary>Gets or sets the roadmap categories.</summary>
        public List<RoadmapCategory> Categories { get; set; } = new List<RoadmapCategory>();
    }
}
