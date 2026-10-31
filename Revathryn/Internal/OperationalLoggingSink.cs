using Microsoft.Extensions.Logging;

using Revathryn.Contracts.Reliability;

namespace Revathryn.Internal
{
    /// <summary>Bridges structured operational diagnostics to Microsoft.Extensions.Logging.</summary>
    internal sealed class OperationalLoggingSink : IOperationalLogSink
    {
        private const string LogCategory = "Revathryn.Operational";
        private const string MessageTemplate = "Component={Component} Operation={Operation} CorrelationId={CorrelationId} Message={Message} ExceptionType={ExceptionType} ExceptionMessage={ExceptionMessage} DurationMs={DurationMs} RetryAttempt={RetryAttempt}";

        private readonly ILogger _logger;

        /// <summary>Initialises a new sink.</summary>
        public OperationalLoggingSink(ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);

            _logger = loggerFactory.CreateLogger(LogCategory);
        }

        /// <summary>Writes a structured diagnostic entry.</summary>
        public void Write(OperationalLogEntry entry)
        {
            if (entry is null)
            {
                return;
            }

            _logger.Log(
                MapLevel(entry.Level),
                MessageTemplate,
                entry.Component ?? string.Empty,
                entry.Operation ?? string.Empty,
                entry.CorrelationId ?? string.Empty,
                entry.Message ?? string.Empty,
                entry.ExceptionType ?? string.Empty,
                entry.ExceptionMessage ?? string.Empty,
                entry.Duration?.TotalMilliseconds,
                entry.RetryAttempt);
        }

        private static LogLevel MapLevel(OperationalLogLevel level)
        {
            return level switch
            {
                OperationalLogLevel.Debug => LogLevel.Debug,
                OperationalLogLevel.Information => LogLevel.Information,
                OperationalLogLevel.Warning => LogLevel.Warning,
                OperationalLogLevel.Error => LogLevel.Error,
                OperationalLogLevel.Critical => LogLevel.Critical,
                _ => LogLevel.Information
            };
        }
    }
}
