namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// Represents system uptime information.
    /// </summary>
    public sealed class UptimeModel
    {
        /// <summary>
        /// 
        /// </summary>
        public UptimeModel()
        {
            StartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Date and time when the system was started. This can be used to calculate the uptime of the system by comparing it with the current date and time.
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan Duration => DateTime.UtcNow - StartTime;
    }
}
