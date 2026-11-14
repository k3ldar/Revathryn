namespace Revathryn.Contracts.Reliability
{
    /// <summary>
    /// Limits concurrent operations per named key so each AI engine can be constrained independently.
    /// </summary>
    public interface IKeyedConcurrencyLimiter
    {
        /// <summary>Acquires a permit for the supplied key. Dispose the result to release the permit.</summary>
        /// <param name="key">The key identifying the resource to limit, such as an AI engine.</param>
        /// <param name="maximumConcurrency">The maximum number of permits for this key.</param>
        /// <param name="cancellationToken">A token that cancels waiting for a permit.</param>
        Task<IDisposable> AcquireAsync(string key, int maximumConcurrency, CancellationToken cancellationToken = default);
    }
}
