namespace Revathryn.Contracts.Enums
{
    /// <summary>Reasons an isolated prompt test run may be rejected before execution.</summary>
    public enum PromptTestRunRejection
    {
        /// <summary>No rejection reason recorded.</summary>
        None = 0,

        /// <summary>The prompt version being tested does not exist.</summary>
        PromptVersionMissing = 1,

        /// <summary>The original repository context could not be retrieved.</summary>
        ContextMissing = 2,

        /// <summary>The detection engine is not configured or enabled.</summary>
        DetectionEngineUnavailable = 3,

        /// <summary>An active test run already exists for the same version and context.</summary>
        Duplicate = 4,

        /// <summary>The test run to retry does not exist or has not failed.</summary>
        NotRetryable = 5
    }
}
