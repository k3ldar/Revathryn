namespace Revathryn.Models
{
    /// <summary>A single breadcrumb navigation entry.</summary>
    public sealed class BreadcrumbItem
    {
        /// <summary>Display text for the breadcrumb.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional navigation URL; null for the current page.</summary>
        public string Url { get; set; }

        /// <summary>Whether the breadcrumb represents the current page.</summary>
        public bool IsActive { get; set; }
    }
}
