using Revathryn.Contracts.Models;

namespace Revathryn.Models
{
    /// <summary>Data displayed on the administration dashboard.</summary>
    public class DashboardViewModel
    {
        /// <summary>Registered repositories.</summary>
        public IReadOnlyList<Repository> Repositories { get; set; } = new List<Repository>();
    }
}
