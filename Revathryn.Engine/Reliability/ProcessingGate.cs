using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Thread-safe processing gate used to halt work during fatal configuration failures.</summary>
    public sealed class ProcessingGate : IProcessingGate
    {
        private readonly object _syncRoot = new();
        private bool _isPaused;
        private string _reason = string.Empty;

        /// <inheritdoc />
        public bool IsPaused
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isPaused;
                }
            }
        }

        /// <inheritdoc />
        public string Reason
        {
            get
            {
                lock (_syncRoot)
                {
                    return _reason;
                }
            }
        }

        /// <inheritdoc />
        public void Pause(string reason)
        {
            lock (_syncRoot)
            {
                _isPaused = true;
                _reason = reason ?? string.Empty;
            }
        }

        /// <inheritdoc />
        public void Resume()
        {
            lock (_syncRoot)
            {
                _isPaused = false;
                _reason = string.Empty;
            }
        }
    }
}
