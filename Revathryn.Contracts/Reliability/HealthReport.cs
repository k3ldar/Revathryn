using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>Aggregate operational health report.</summary>
    public sealed class HealthReport
    {
        /// <summary>Overall status derived from the worst individual result.</summary>
        public HealthStatus Status { get; set; }

        /// <summary>Individual check results.</summary>
        public IReadOnlyList<HealthCheckResult> Checks { get; set; } = [];

        /// <summary>UTC timestamp when the report was produced.</summary>
        public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    }
}
