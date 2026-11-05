namespace Revathryn.Contracts.Enums
{
    /// <summary>Lifecycle of a queued duplicate-detection work item.</summary>
    public enum DuplicateDetectionStatus
    {
        /// <summary>Waiting to be claimed by a worker.</summary>
        Pending = 0,

        /// <summary>Currently being processed under a lease.</summary>
        Running = 1,

        /// <summary>Processing completed successfully.</summary>
        Completed = 2,

        /// <summary>Processing failed permanently.</summary>
        Failed = 3
    }
}
