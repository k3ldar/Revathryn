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
    }
}
