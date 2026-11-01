using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>A transient pending review operation.</summary>
    public class QueueItem
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The file to review.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type to execute.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>Processing priority; higher values are processed first.</summary>
        public int Priority { get; set; }

        /// <summary>UTC timestamp when the item entered the queue.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>Number of processing attempts already made.</summary>
        public int AttemptCount { get; set; }

        /// <summary>Maximum processing attempts before dead-lettering; zero uses the policy default.</summary>
        public int MaxAttempts { get; set; }

        /// <summary>UTC timestamp before which the item must not be claimed, or null when immediately eligible.</summary>
        public DateTime? NextAttemptUtc { get; set; }

        /// <summary>Message from the most recent failure, if any.</summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the current processing lease expires, or null when not leased.</summary>
        public DateTime? LeasedUntilUtc { get; set; }

        /// <summary>Current lifecycle state of the item.</summary>
        public QueueItemStatus Status { get; set; }
    }
}
