namespace Revathryn.Contracts.Reliability
{
    /// <summary>Summary of work recovered during startup reconciliation.</summary>
    public sealed class RecoveryResult
    {
        /// <summary>Number of queue items found pending at startup.</summary>
        public int PendingQueueItems { get; set; }

        /// <summary>Number of queue items removed because they were already satisfied.</summary>
        public int RemovedDuplicates { get; set; }

        /// <summary>Number of queue items confirmed as still requiring review.</summary>
        public int Retained { get; set; }

        /// <summary>Whether recovery completed without failure.</summary>
        public bool Succeeded { get; set; }
    }
}
