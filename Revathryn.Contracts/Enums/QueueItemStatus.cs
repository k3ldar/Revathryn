namespace Revathryn.Contracts.Enums
{
    /// <summary>Lifecycle state of a queued review operation.</summary>
    public enum QueueItemStatus
    {
        /// <summary>Waiting to be claimed and processed.</summary>
        Pending = 0,

        /// <summary>Currently leased to a processor.</summary>
        InFlight = 1
    }
}
