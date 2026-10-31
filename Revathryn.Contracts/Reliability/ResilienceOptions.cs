using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>Central configuration for reliability behaviour.</summary>
    public sealed class ResilienceOptions
    {
        private const int DefaultShutdownTimeout = 30;

        /// <summary>Default retry policy applied when no operation-specific policy exists.</summary>
        public RetryPolicyOptions DefaultRetry { get; set; } = new();

        /// <summary>Timeout applied when gracefully shutting down active work.</summary>
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(DefaultShutdownTimeout);

        /// <summary>Per-operation retry policy overrides keyed by operation type.</summary>
        public Dictionary<RetryOperationType, RetryPolicyOptions> OperationRetryPolicies { get; } = [];

        /// <summary>Returns the retry policy options for the supplied operation type.</summary>
        public RetryPolicyOptions GetRetryPolicy(RetryOperationType operationType)
        {
            if (OperationRetryPolicies.TryGetValue(operationType, out RetryPolicyOptions policy))
            {
                return policy;
            }

            return DefaultRetry;
        }
    }
}
