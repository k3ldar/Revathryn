namespace Revathryn.Contracts.Review
{
    /// <summary>A single unit of additional context supplied to a review.</summary>
    public class ContextItem
    {
        /// <summary>Short title of the context item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Text content of the context item.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Optional source path the context item originated from.</summary>
        public string SourcePath { get; set; } = string.Empty;
    }
}
