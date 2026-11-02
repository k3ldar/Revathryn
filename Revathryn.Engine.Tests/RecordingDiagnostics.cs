using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Tests
{
    /// <summary>Diagnostics sink that records messages so tests can assert on them.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class RecordingDiagnostics : IOperationalDiagnostics
    {
        public List<string> Messages { get; } = [];

        public void Log(
            OperationalLogLevel level,
            string component,
            string operation,
            string message,
            Exception exception = null,
            TimeSpan? duration = null,
            int? retryAttempt = null)
        {
            Messages.Add(message);
        }
    }
}
