namespace Revathryn.Contracts.Models
{
    /// <summary>Controls which review types apply to which files.</summary>
    public class ReviewLink
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The linked file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The linked review type.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>Whether the review type applies to the file.</summary>
        public bool Enabled { get; set; }
    }
}
