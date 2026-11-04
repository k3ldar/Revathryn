namespace Revathryn.Contracts.Analysis
{
    /// <summary>Ranked symbol candidates for a query.</summary>
    public sealed class SymbolQueryResponse
    {
        /// <summary>Echoes the requested context key.</summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>Candidates ordered by descending relevance.</summary>
        public List<SymbolCandidate> Candidates { get; set; } = [];
    }
}
