namespace Revathryn.Contracts.Enums
{
    /// <summary>Execution state of a prompt optimisation. This never changes the active prompt by itself.</summary>
    public enum PromptOptimisationStatus
    {
        /// <summary>The optimisation is waiting to be executed.</summary>
        Pending = 0,

        /// <summary>The optimisation is currently executing.</summary>
        Running = 1,

        /// <summary>The analysis and recommendations completed successfully.</summary>
        Completed = 2,

        /// <summary>The optimisation did not complete successfully.</summary>
        Failed = 3
    }
}
