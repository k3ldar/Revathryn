using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>Pairs a completed analysis record with its review type name for display.</summary>
    public sealed class ReviewOutcomeViewModel
    {
        /// <summary>The completed analysis record.</summary>
        public RepoFileAnalysis Analysis { get; set; }

        /// <summary>Display name of the review type that produced the outcome.</summary>
        public string ReviewTypeName { get; set; } = string.Empty;
    }
}
