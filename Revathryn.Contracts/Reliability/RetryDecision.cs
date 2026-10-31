using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>The result of evaluating whether a failed operation should be retried.</summary>
    public sealed class RetryDecision
    {
        /// <summary>Whether the operation should be retried.</summary>
        public bool ShouldRetry { get; init; }

        /// <summary>The delay to apply before the next attempt.</summary>
        public TimeSpan Delay { get; init; }

        /// <summary>The classified category that drove the decision.</summary>
        public FailureCategory Category { get; init; }

        /// <summary>Creates a decision that stops retrying immediately.</summary>
        public static RetryDecision Stop(FailureCategory category)
        {
            return new RetryDecision { ShouldRetry = false, Delay = TimeSpan.Zero, Category = category };
        }

        /// <summary>Creates a decision that retries after the supplied delay.</summary>
        public static RetryDecision Retry(TimeSpan delay, FailureCategory category)
        {
            return new RetryDecision { ShouldRetry = true, Delay = delay, Category = category };
        }
    }
}
