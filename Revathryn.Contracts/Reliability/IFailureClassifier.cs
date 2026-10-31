using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Reliability
{
    /// <summary>Classifies exceptions into failure categories for consistent handling.</summary>
    public interface IFailureClassifier
    {
        /// <summary>Classifies the supplied exception.</summary>
        FailureCategory Classify(Exception exception);

        /// <summary>Whether the exception represents a retryable transient failure.</summary>
        bool IsRetryable(Exception exception);
    }
}
