namespace Revathryn.Contracts.Enums
{
    /// <summary>Progress phases of a repository scan.</summary>
    public enum ScanPhase
    {
        /// <summary>Files are being discovered and fingerprinted.</summary>
        Discovering = 0,

        /// <summary>The repository inventory is being reconciled.</summary>
        Reconciling = 1,

        /// <summary>Source analysis is running.</summary>
        Analysing = 2,

        /// <summary>Review work is being scheduled.</summary>
        Scheduling = 3,

        /// <summary>The scan has completed successfully.</summary>
        Completed = 4
    }
}
