using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Provides cached retry policies per operation type.</summary>
    public sealed class RetryPolicyProvider : IRetryPolicyProvider
    {
        private readonly ResilienceOptions _options;
        private readonly IFailureClassifier _failureClassifier;
        private readonly Dictionary<RetryOperationType, IRetryPolicy> _policies = [];

        /// <summary>Initialises a new provider.</summary>
        public RetryPolicyProvider(ResilienceOptions options, IFailureClassifier failureClassifier)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(failureClassifier);

            _options = options;
            _failureClassifier = failureClassifier;
        }

        /// <summary>Returns the retry policy for the supplied operation type.</summary>
        public IRetryPolicy GetPolicy(RetryOperationType operationType)
        {
            if (_policies.TryGetValue(operationType, out IRetryPolicy existing))
            {
                return existing;
            }

            RetryPolicyOptions policyOptions = _options.GetRetryPolicy(operationType);
            IRetryPolicy policy = new ExponentialBackoffRetryPolicy(policyOptions, _failureClassifier);
            _policies[operationType] = policy;
            return policy;
        }
    }
}
