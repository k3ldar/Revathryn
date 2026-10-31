using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Writes structured operational diagnostics to every registered sink.</summary>
    public sealed class OperationalDiagnostics : IOperationalDiagnostics
    {
        private readonly ICorrelationContext _correlationContext;
        private readonly IReadOnlyList<IOperationalLogSink> _sinks;

        /// <summary>Initialises a new diagnostics writer.</summary>
        public OperationalDiagnostics(ICorrelationContext correlationContext, IEnumerable<IOperationalLogSink> sinks)
        {
            _correlationContext = correlationContext;
            _sinks = [.. (sinks ?? [])];
        }

        /// <summary>Records a structured diagnostic entry.</summary>
        public void Log(
            OperationalLogLevel level,
            string component,
            string operation,
            string message,
            Exception exception = null,
            TimeSpan? duration = null,
            int? retryAttempt = null)
        {
            OperationalLogEntry entry = new()
            {
                Level = level,
                Component = component ?? string.Empty,
                Operation = operation ?? string.Empty,
                CorrelationId = _correlationContext.Current,
                Message = message ?? string.Empty,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception?.Message,
                Duration = duration,
                RetryAttempt = retryAttempt
            };

            foreach (IOperationalLogSink sink in _sinks)
            {
                sink.Write(entry);
            }
        }
    }
}
