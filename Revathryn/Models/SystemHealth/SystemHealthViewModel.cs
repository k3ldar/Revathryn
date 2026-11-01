using Revathryn.Contracts.Reliability;

namespace Revathryn.Models.SystemHealth
{
    /// <summary>Data displayed on the system health page.</summary>
    public sealed class SystemHealthViewModel
    {
        /// <summary>The readiness health report.</summary>
        public HealthReport HealthReport { get; set; } = new();

        /// <summary>UTC timestamp when the application started.</summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>Elapsed time since the application started.</summary>
        public TimeSpan Uptime { get; set; }
    }
}
