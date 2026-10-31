namespace Revathryn.Contracts.Reliability
{
    /// <summary>A structured operational diagnostic record.</summary>
    public sealed class OperationalLogEntry
    {
        /// <summary>UTC timestamp of the event.</summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Severity of the event.</summary>
        public OperationalLogLevel Level { get; set; }

        /// <summary>Component that produced the event.</summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>Operation that was being performed.</summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>Correlation identifier linking related work.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Human-readable message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Exception type when an exception is attached.</summary>
        public string ExceptionType { get; set; }

        /// <summary>Exception message when an exception is attached.</summary>
        public string ExceptionMessage { get; set; }

        /// <summary>Optional processing duration.</summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>Optional retry attempt number.</summary>
        public int? RetryAttempt { get; set; }
    }
}
