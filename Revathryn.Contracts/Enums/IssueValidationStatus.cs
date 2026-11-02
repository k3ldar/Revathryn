namespace Revathryn.Contracts.Enums
{
    /// <summary>Execution state of an issue validation. This is not the lifecycle status of the issue.</summary>
    public enum IssueValidationStatus
    {
        /// <summary>The validation is waiting to be executed.</summary>
        Pending = 0,

        /// <summary>The validation is currently executing.</summary>
        Running = 1,

        /// <summary>The review engine completed its assessment and a verdict was recorded.</summary>
        Completed = 2,

        /// <summary>The validation process did not complete successfully. No verdict exists.</summary>
        Failed = 3
    }
}
