namespace Revathryn.Contracts.Reliability
{
    /// <summary>Configurable retry policy parameters for an operation type.</summary>
    public sealed class RetryPolicyOptions
    {
        /// <summary>Maximum number of attempts before retries are exhausted.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Initial delay before the first retry.</summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Upper bound applied to computed backoff delays.</summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Multiplier applied to the delay after each failed attempt.</summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>Fraction of jitter (0 to 1) applied to avoid synchronised retry storms.</summary>
        public double JitterFactor { get; set; } = 0.25;
    }
}
