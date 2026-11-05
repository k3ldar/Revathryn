using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A queued request to evaluate a finding for duplicate similarity.</summary>
    public sealed class DuplicateDetectionQueueItem
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository used to scope candidate matching.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The finding to evaluate.</summary>
        public long FindingId { get; set; }

        /// <summary>Current lifecycle state.</summary>
        public DuplicateDetectionStatus Status { get; set; }

        /// <summary>Number of processing attempts made.</summary>
        public int AttemptCount { get; set; }

        /// <summary>UTC timestamp when the item was enqueued.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Earliest UTC time the item may be claimed, or null when immediately eligible.</summary>
        public DateTime? NextAttemptUtc { get; set; }

        /// <summary>UTC time until which the item is leased to a worker, or null when unclaimed.</summary>
        public DateTime? LeasedUntilUtc { get; set; }

        /// <summary>Most recent processing error, when any.</summary>
        public string LastError { get; set; } = string.Empty;
    }
}
