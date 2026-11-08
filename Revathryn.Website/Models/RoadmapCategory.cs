namespace Revathryn.Website.Models
{
    /// <summary>Represents a group of related roadmap items.</summary>
    public sealed class RoadmapCategory
    {
        /// <summary>Gets or sets the category name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the items within the category.</summary>
        public List<RoadmapItem> Items { get; set; } = new List<RoadmapItem>();
    }
}
