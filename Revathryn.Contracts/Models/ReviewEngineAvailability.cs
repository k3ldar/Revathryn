namespace Revathryn.Contracts.Models
{
    /// <summary>Whether independent validation can currently be requested.</summary>
    public sealed class ReviewEngineAvailability
    {
        /// <summary>True when the review engine is configured, valid and enabled.</summary>
        public bool IsAvailable { get; set; }

        /// <summary>An explanation, safe to display, when unavailable.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
