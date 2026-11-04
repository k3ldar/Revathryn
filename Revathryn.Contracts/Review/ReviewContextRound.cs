namespace Revathryn.Contracts.Review
{
    /// <summary>A single round of context the AI requested during a review.</summary>
    public sealed class ReviewContextRound
    {
        /// <summary>Context keys requested for this round, preserved in request order.</summary>
        public List<string> RequestedKeys { get; set; } = [];

        /// <summary>Rendered context items supplied for this round.</summary>
        public List<string> Items { get; set; } = [];
    }
}
