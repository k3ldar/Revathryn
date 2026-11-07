namespace Revathryn.Contracts.Analysis
{
    /// <summary>Produces free-text descriptive structure summaries for files.</summary>
    public interface IArchitectureAnalysisEngine
    {
        /// <summary>Produces a free-text descriptive structure summary for a single file.</summary>
        Task<ArchitectureAnalysisResponse> AnalyseStructureAsync(ArchitectureAnalysisRequest request, CancellationToken cancellationToken = default);
    }
}
