namespace Revathryn.Contracts.Reliability
{
    /// <summary>Severity levels for structured operational diagnostics.</summary>
    public enum OperationalLogLevel
    {
        /// <summary>Verbose diagnostic detail.</summary>
        Debug = 0,

        /// <summary>Routine operational information.</summary>
        Information = 1,

        /// <summary>A degraded condition that should be monitored.</summary>
        Warning = 2,

        /// <summary>A failure that requires attention.</summary>
        Error = 3,

        /// <summary>A critical failure affecting system capability.</summary>
        Critical = 4
    }
}
