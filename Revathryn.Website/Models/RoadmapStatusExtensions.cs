namespace Revathryn.Website.Models
{
    /// <summary>Presentation helpers for roadmap status values.</summary>
    public static class RoadmapStatusExtensions
    {
        /// <summary>Returns the display label for a status.</summary>
        public static string GetDisplayName(this RoadmapStatus status)
        {
            return status switch
            {
                RoadmapStatus.Completed => "Completed",
                RoadmapStatus.InProgress => "In Progress",
                RoadmapStatus.Planned => "Planned",
                RoadmapStatus.UnderConsideration => "Under Consideration",
                _ => status.ToString()
            };
        }

        /// <summary>Returns the CSS class for a status badge.</summary>
        public static string GetCssClass(this RoadmapStatus status)
        {
            return status switch
            {
                RoadmapStatus.Completed => "status-completed",
                RoadmapStatus.InProgress => "status-inprogress",
                RoadmapStatus.Planned => "status-planned",
                RoadmapStatus.UnderConsideration => "status-consideration",
                _ => "status-consideration"
            };
        }
    }
}
