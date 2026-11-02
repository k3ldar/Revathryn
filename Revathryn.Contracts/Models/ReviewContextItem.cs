namespace Revathryn.Contracts.Models
{
    /// <summary>A single unit of additional context supplied to the AI, preserved exactly as supplied.</summary>
    public sealed class ReviewContextItem
    {
        /// <summary>Short title of the context item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional source path the context item originated from.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>The rendered content that was supplied to the AI.</summary>
        public string Content { get; set; } = string.Empty;
    }
}
