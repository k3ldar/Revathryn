using System.Diagnostics;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Executes operations with configurable retry behaviour and diagnostics.</summary>
    public sealed class RetryExecutor : IRetryExecutor
    {
        private const string OperationSucceededMessage = "Operation completed successfully.";
        private const string OperationRetryMessage = "Transient failure; the operation will be retried.";
        private const string OperationRetryExhaustedMessage = "The operation failed and retry attempts were exhausted.";

        private readonly IRetryPolicyProvider _policyProvider;
        private readonly IOperationalDiagnostics _diagnostics;

        /// <summary>Initialises a new executor.</summary>
        public RetryExecutor(IRetryPolicyProvider policyProvider, IOperationalDiagnostics diagnostics)
        {
            ArgumentNullException.ThrowIfNull(policyProvider);
            ArgumentNullException.ThrowIfNull(diagnostics);

            _policyProvider = policyProvider;
            _diagnostics = diagnostics;
        }

        /// <summary>Executes a value-returning operation with the policy for the supplied operation type.</summary>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            RetryOperationType operationType,
            string component,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            IRetryPolicy policy = _policyProvider.GetPolicy(operationType);
            int attempt = 0;

            while (true)
            {
                attempt++;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    TResult result = await operation(cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();

                    _diagnostics.Log(
                        OperationalLogLevel.Debug,
                        component,
                        operationName,
                        OperationSucceededMessage,
                        duration: stopwatch.Elapsed,
                        retryAttempt: attempt);

                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    RetryDecision decision = policy.Evaluate(attempt, exception);

                    if (decision.ShouldRetry)
                    {
                        _diagnostics.Log(
                            OperationalLogLevel.Warning,
                            component,
                            operationName,
                            OperationRetryMessage,
                            exception,
                            retryAttempt: attempt);

                        await Task.Delay(decision.Delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    _diagnostics.Log(
                        OperationalLogLevel.Error,
                        component,
                        operationName,
                        OperationRetryExhaustedMessage,
                        exception,
                        retryAttempt: attempt);

                    throw;
                }
            }
        }

        /// <summary>Executes a void operation with the policy for the supplied operation type.</summary>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            RetryOperationType operationType,
            string component,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            await ExecuteAsync<object>(
                async token =>
                {
                    await operation(token).ConfigureAwait(false);
                    return null;
                },

                operationType,
                component,
                operationName,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
