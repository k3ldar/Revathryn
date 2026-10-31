namespace Revathryn.Contracts.Reliability
{
    /// <summary>Determines whether a failed operation should be retried and after how long.</summary>
    public interface IRetryPolicy
    {
        /// <summary>Evaluates a failure at the supplied one-based attempt number.</summary>
        RetryDecision Evaluate(int attempt, Exception exception);
    }
}
