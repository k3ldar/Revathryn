namespace Revathryn.Contracts.Reliability
{
    /// <summary>Writes structured operational diagnostics.</summary>
    public interface IOperationalDiagnostics
    {
        /// <summary>Records a structured diagnostic entry.</summary>
        void Log(
            OperationalLogLevel level,
            string component,
            string operation,
            string message,
            Exception exception = null,
            TimeSpan? duration = null,
            int? retryAttempt = null);
    }
}
