namespace Revathryn.Contracts.Reliability
{
    /// <summary>Configurable retry policy parameters for an operation type.</summary>
    public sealed class RetryPolicyOptions
    {
        private const int MaxDeleySeconds = 30;
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 200;
        private const double DefaultBackoffMultiplier = 2.0;
        private const double DefaultJitterFactor = 0.25;

        /// <summary>Maximum number of attempts before retries are exhausted.</summary>
        public int MaxAttempts { get; set; } = MaxRetries;

        /// <summary>Initial delay before the first retry.</summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(InitialDelayMs);

        /// <summary>Upper bound applied to computed backoff delays.</summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(MaxDeleySeconds);

        /// <summary>Multiplier applied to the delay after each failed attempt.</summary>
        public double BackoffMultiplier { get; set; } = DefaultBackoffMultiplier;

        /// <summary>Fraction of jitter (0 to 1) applied to avoid synchronised retry storms.</summary>
        public double JitterFactor { get; set; } = DefaultJitterFactor;
    }
}
