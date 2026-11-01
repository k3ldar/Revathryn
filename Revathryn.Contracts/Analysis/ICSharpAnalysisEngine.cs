namespace Revathryn.Contracts.Analysis
{
    /// <summary>C#-specific analysis engine that wraps the Roslyn-based analyser.</summary>
    public interface ICSharpAnalysisEngine : IAnalysisEngine
    {
        /// <summary>Returns true when the file needs (re)analysis according to the repository's manifest.</summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="filePath">Absolute path of the source file.</param>
        bool RequiresAnalysis(string repositoryName, string filePath);

        /// <summary>Analyses C# source and returns the extracted symbols as JSON.</summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="filePath">Absolute path of the source file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<string> AnalyseSymbolsAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default);
    }
}
