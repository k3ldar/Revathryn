using Revathryn.Models.Dashboard;

namespace Revathryn.Models
{
    /// <summary>Data displayed on the administration dashboard.</summary>
    public class DashboardViewModel
    {
        /// <summary>Repository-level review lifecycle summaries.</summary>
        public IReadOnlyList<RepositorySummaryViewModel> Repositories { get; set; } = [];
    }
}
