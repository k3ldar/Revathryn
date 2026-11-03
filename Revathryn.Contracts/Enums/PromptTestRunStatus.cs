namespace Revathryn.Contracts.Enums
{
    /// <summary>Execution state of an isolated prompt test run.</summary>
    public enum PromptTestRunStatus
    {
        /// <summary>The test is waiting to be executed.</summary>
        Pending = 0,

        /// <summary>The test is currently executing.</summary>
        Running = 1,

        /// <summary>The test completed and produced a comparison.</summary>
        Completed = 2,

        /// <summary>The test did not complete successfully.</summary>
        Failed = 3
    }
}
