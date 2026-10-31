using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>The outcome of a single health check.</summary>
    public sealed class HealthCheckResult
    {
        /// <summary>Name of the checked capability.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Observed status.</summary>
        public HealthStatus Status { get; set; }

        /// <summary>Optional UTC timestamp of the last successful check.</summary>
        public DateTime? LastSuccessfulUtc { get; set; }

        /// <summary>Optional diagnostic detail.</summary>
        public string Detail { get; set; }
    }
}
