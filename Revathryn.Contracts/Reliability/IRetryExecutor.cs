using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>Executes operations with configurable retry behaviour.</summary>
    public interface IRetryExecutor
    {
        /// <summary>Executes a value-returning operation with the policy for the supplied operation type.</summary>
        Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            RetryOperationType operationType,
            string component,
            string operationName,
            CancellationToken cancellationToken = default);

        /// <summary>Executes a void operation with the policy for the supplied operation type.</summary>
        Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            RetryOperationType operationType,
            string component,
            string operationName,
            CancellationToken cancellationToken = default);
    }
}
