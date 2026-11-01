namespace Revathryn.Contracts.Models
{
    /// <summary>A page of findings together with the total matching count.</summary>
    public sealed class FindingQueryResult
    {
        /// <summary>Findings in the requested page.</summary>
        public IReadOnlyList<Finding> Items { get; set; } = [];

        /// <summary>Total number of findings matching the query, ignoring paging.</summary>
        public int TotalCount { get; set; }
    }
}
