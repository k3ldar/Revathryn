namespace Revathryn.Contracts.Review
{
    /// <summary>Outcome of a review execution.</summary>
    public class ReviewResponse
    {
        /// <summary>Whether the review completed without an operational error.</summary>
        public bool Success { get; set; }

        /// <summary>Optional error message when <see cref="Success"/> is false.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Findings produced by the review. Empty when no issues were identified.</summary>
        public List<ReviewResult> Results { get; set; } = [];
    }
}
