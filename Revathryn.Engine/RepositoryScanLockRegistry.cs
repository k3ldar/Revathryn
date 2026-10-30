using System.Collections.Concurrent;

namespace Revathryn.Engine
{
    /// <summary>Provides a per-repository scan lock so each repository is scanned one at a time.</summary>
    public sealed class RepositoryScanLockRegistry
    {
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

        /// <summary>Returns the semaphore guarding scans for the given repository.</summary>
        public SemaphoreSlim GetLock(long repositoryId)
        {
            return _locks.GetOrAdd(repositoryId, _ => new SemaphoreSlim(1, 1));
        }
    }
}