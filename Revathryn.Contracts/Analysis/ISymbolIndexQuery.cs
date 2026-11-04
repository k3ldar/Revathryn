namespace Revathryn.Contracts.Analysis
{
    /// <summary>Answers symbol and file candidate queries against the per-repository index.</summary>
    public interface ISymbolIndexQuery
    {
        /// <summary>Returns ranked symbol candidates for the supplied request.</summary>
        /// <param name="repositoryName">Name of the repository to query.</param>
        /// <param name="request">Query parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<SymbolQueryResponse> QueryAsync(string repositoryName, SymbolQueryRequest request, CancellationToken cancellationToken = default);
    }
}
