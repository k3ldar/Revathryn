using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>Provides retry policies per operation type.</summary>
    public interface IRetryPolicyProvider
    {
        /// <summary>Returns the retry policy for the supplied operation type.</summary>
        IRetryPolicy GetPolicy(RetryOperationType operationType);
    }
}
