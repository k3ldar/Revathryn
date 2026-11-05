namespace Revathryn.Contracts.Models
{
    /// <summary>The persistence plan produced by reconciling a review against existing findings.</summary>
    public sealed class FindingReconciliation
    {
        /// <summary>New findings with no matching prior finding that should be inserted.</summary>
        public IReadOnlyList<Finding> FindingsToInsert { get; set; } = [];

        /// <summary>Prior findings whose content should be refreshed while retaining their triage status.</summary>
        public IReadOnlyList<Finding> FindingsToUpdate { get; set; } = [];

        /// <summary>Prior open findings that should be auto-resolved because the review no longer reported them.</summary>
        public IReadOnlyList<Finding> FindingsToResolve { get; set; } = [];
    }
}
