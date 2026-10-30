namespace Revathryn.Contracts.Review
{
    /// <summary>Response carrying additional context for a review.</summary>
    public class ReviewContextResponse
    {
        /// <summary>Echoes the requested context key.</summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>Context items produced by the provider.</summary>
        public List<ContextItem> Items { get; set; } = [];
    }
}
