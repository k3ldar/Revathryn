namespace Revathryn.Contracts.Enums
{
    /// <summary>Operational health state of the application or a dependency.</summary>
    public enum HealthStatus
    {
        /// <summary>Fully capable of performing its intended work.</summary>
        Healthy = 0,

        /// <summary>Operating with reduced capability.</summary>
        Degraded = 1,

        /// <summary>Not capable of performing its intended work.</summary>
        Unavailable = 2
    }
}
