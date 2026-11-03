namespace Revathryn.Contracts.Enums
{
    /// <summary>Reasons a prompt optimisation request may be rejected before execution.</summary>
    public enum PromptOptimisationRejection
    {
        /// <summary>No rejection reason recorded.</summary>
        None = 0,

        /// <summary>The originating issue does not have a completed validation.</summary>
        ValidationMissing = 1,

        /// <summary>The review engine is not configured or enabled.</summary>
        ReviewEngineUnavailable = 2,

        /// <summary>An active optimisation already exists for the same validation.</summary>
        Duplicate = 3,

        /// <summary>The original detection context is missing or incomplete.</summary>
        ContextMissing = 4,

        /// <summary>The originating issue does not exist.</summary>
        IssueNotFound = 5,

        /// <summary>The repository owning the issue does not exist.</summary>
        RepositoryNotFound = 6,

        /// <summary>The optimisation to retry does not exist or has not failed.</summary>
        NotRetryable = 7
    }
}
