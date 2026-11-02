namespace Revathryn.Contracts.Enums
{
    /// <summary>The reason a request for validation was rejected.</summary>
    public enum IssueValidationRejection
    {
        /// <summary>The issue does not exist.</summary>
        IssueNotFound = 0,

        /// <summary>The repository owning the issue does not exist.</summary>
        RepositoryNotFound = 1,

        /// <summary>The review engine is not configured, valid or enabled.</summary>
        ReviewEngineUnavailable = 2,

        /// <summary>The validation to retry does not exist or has not failed.</summary>
        NotRetryable = 3,

        /// <summary>The issue is an operational failure rather than a code finding and cannot be validated.</summary>
        NotCodeIssue = 4
    }
}
