using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Retry policy using exponential backoff with full jitter.</summary>
    public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private const double JitterFullRangeMultiplier = 2.0;

        private readonly RetryPolicyOptions _options;
        private readonly IFailureClassifier _failureClassifier;

        /// <summary>Initialises a new policy.</summary>
        public ExponentialBackoffRetryPolicy(RetryPolicyOptions options, IFailureClassifier failureClassifier)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(failureClassifier);

            _options = options;
            _failureClassifier = failureClassifier;
        }

        /// <summary>Evaluates a failure at the supplied one-based attempt number.</summary>
        public RetryDecision Evaluate(int attempt, Exception exception)
        {
            if (exception is null)
            {
                return RetryDecision.Stop(FailureCategory.Unexpected);
            }

            FailureCategory category = _failureClassifier.Classify(exception);

            if (category is not FailureCategory.Transient)
            {
                return RetryDecision.Stop(category);
            }

            if (attempt >= _options.MaxAttempts)
            {
                return RetryDecision.Stop(category);
            }

            return RetryDecision.Retry(GetRetryDelay(attempt), category);
        }

        /// <summary>Computes the backoff delay for the supplied one-based attempt number.</summary>
        public TimeSpan GetRetryDelay(int attempt)
        {
            return ComputeDelay(attempt);
        }

        private TimeSpan ComputeDelay(int attempt)
        {
            double multiplier = Math.Pow(_options.BackoffMultiplier, attempt - 1);
            double baseMilliseconds = _options.InitialDelay.TotalMilliseconds * multiplier;
            double cappedMilliseconds = Math.Min(baseMilliseconds, _options.MaxDelay.TotalMilliseconds);
            double jitterRange = cappedMilliseconds * _options.JitterFactor;
            double jitter = (Random.Shared.NextDouble() * JitterFullRangeMultiplier * jitterRange) - jitterRange;

            return TimeSpan.FromMilliseconds(Math.Max(0, cappedMilliseconds + jitter));
        }
    }
}
