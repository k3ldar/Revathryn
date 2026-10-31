using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>
    /// An exception that carries the failure classification and operational context
    /// required for consistent handling, logging and retry decisions.
    /// </summary>
    public class ReliabilityException : Exception
    {
        /// <summary>Initialises a new classified exception.</summary>
        public ReliabilityException(
            FailureCategory category,
            string message,
            string component,
            string operation,
            Exception innerException = null,
            string correlationId = null)
            : base(message, innerException)
        {
            Category = category;
            Component = component ?? string.Empty;
            Operation = operation ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }

        /// <summary>The classified failure category.</summary>
        public FailureCategory Category { get; }

        /// <summary>The component that reported the failure.</summary>
        public string Component { get; }

        /// <summary>The operation that was being performed.</summary>
        public string Operation { get; }

        /// <summary>The correlation identifier for the surrounding work, when available.</summary>
        public string CorrelationId { get; }

        /// <summary>Whether the failure is expected to resolve without intervention.</summary>
        public bool IsTransient => Category is FailureCategory.Transient;
    }
}
