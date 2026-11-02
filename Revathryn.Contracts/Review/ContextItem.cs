namespace Revathryn.Contracts.Review
{
    /// <summary>A single unit of additional context supplied to a review.</summary>
    public class ContextItem
    {
        /// <summary>Short title of the context item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Text content of the context item.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>True when the content was truncated by the provider.</summary>
        public bool Truncated { get; set; }

        /// <summary>Length of the content before truncation. 0 when not truncated.</summary>
        public int OriginalLength { get; set; }

        /// <summary>Optional source path the context item originated from.</summary>
        public string SourcePath { get; set; } = string.Empty;
    }
}
