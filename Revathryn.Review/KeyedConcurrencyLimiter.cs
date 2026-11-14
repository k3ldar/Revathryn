using System.Collections.Concurrent;

using Revathryn.Contracts;
using Revathryn.Contracts.Reliability;

namespace Revathryn.Review
{
    /// <summary>
    /// Keyed semaphore-based limiter. Each key gets its own gate which is resized when the requested limit changes.
    /// </summary>
    public sealed class KeyedConcurrencyLimiter : IKeyedConcurrencyLimiter
    {
        private readonly ConcurrentDictionary<string, GateEntry> _gates = new(StringComparer.Ordinal);

        /// <summary>Acquires a permit for the supplied key and returns it as a disposable.</summary>
        public async Task<IDisposable> AcquireAsync(string key, int maximumConcurrency, CancellationToken cancellationToken = default)
        {
            string gateKey = key ?? string.Empty;
            int limit = Math.Clamp(maximumConcurrency, Consts.MinimumAIMaxSimultaneousConnections, Consts.MaximumAIMaxSimultaneousConnections);

            GateEntry entry = _gates.GetOrAdd(gateKey, static _ => new GateEntry(Consts.MinimumAIMaxSimultaneousConnections));
            SemaphoreSlim gate = entry.GetGate(limit);

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ReleasePermit(gate);
        }

        private sealed class GateEntry
        {
            private readonly object _sync = new();
            private SemaphoreSlim _gate;
            private int _maximum;

            public GateEntry(int maximum)
            {
                _maximum = maximum;
                _gate = new SemaphoreSlim(maximum, maximum);
            }

            public SemaphoreSlim GetGate(int maximum)
            {
                lock (_sync)
                {
                    if (maximum != _maximum)
                    {
                        _maximum = maximum;
                        _gate = new SemaphoreSlim(maximum, maximum);
                    }

                    return _gate;
                }
            }
        }

        private sealed class ReleasePermit : IDisposable
        {
            private SemaphoreSlim _gate;

            public ReleasePermit(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
            }
        }
    }
}
