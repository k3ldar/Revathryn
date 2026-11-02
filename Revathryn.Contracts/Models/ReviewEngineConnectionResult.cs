namespace Revathryn.Contracts.Models
{
    /// <summary>The result of testing the review engine connection.</summary>
    public sealed class ReviewEngineConnectionResult
    {
        /// <summary>Whether the provider responded successfully.</summary>
        public bool Success { get; set; }

        /// <summary>A message safe to display. Never contains credentials.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
