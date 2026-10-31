using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Maintains an asynchronous correlation identifier for related work.</summary>
    public sealed class CorrelationContext : ICorrelationContext
    {
        private const string NoSeparatorsGuidFormat = "N";

        private static readonly AsyncLocal<string> CurrentCorrelation = new();

        /// <summary>The current correlation identifier, or an empty string.</summary>
        public string Current => CurrentCorrelation.Value ?? string.Empty;

        /// <summary>Begins a scope with the supplied correlation identifier, the current one, or a new identifier.</summary>
        public IDisposable Begin(string correlationId = null)
        {
            string previous = CurrentCorrelation.Value;

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                CurrentCorrelation.Value = correlationId;
            }
            else if (string.IsNullOrEmpty(previous))
            {
                CurrentCorrelation.Value = Guid.NewGuid().ToString(NoSeparatorsGuidFormat);
            }

            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            internal Scope(string previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                CurrentCorrelation.Value = _previous;
                _disposed = true;
            }
        }
    }
}
