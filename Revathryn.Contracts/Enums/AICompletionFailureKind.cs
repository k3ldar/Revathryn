namespace Revathryn.Contracts.Enums
{
    /// <summary>Classifies why an AI completion request did not produce content.</summary>
    public enum AICompletionFailureKind
    {
        /// <summary>The request succeeded.</summary>
        None = 0,

        /// <summary>The provider did not respond within the allowed time.</summary>
        Timeout = 1,

        /// <summary>The provider rejected the request because of rate limiting.</summary>
        RateLimited = 2,

        /// <summary>A transient provider or network failure that may succeed on retry.</summary>
        Transient = 3,

        /// <summary>The provider rejected the credentials.</summary>
        Authentication = 4,

        /// <summary>The provider rejected the request itself, or returned no usable content.</summary>
        Rejected = 5,

        /// <summary>The engine configuration is missing or unusable.</summary>
        Configuration = 6
    }
}
