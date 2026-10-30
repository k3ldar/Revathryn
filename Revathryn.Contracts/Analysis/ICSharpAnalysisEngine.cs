namespace Revathryn.Contracts.Analysis
{
    /// <summary>C#-specific analysis engine that wraps the Roslyn-based analyser.</summary>
    public interface ICSharpAnalysisEngine : IAnalysisEngine
    {
        /// <summary>Returns true when the file needs (re)analysis according to the manifest.</summary>
        bool RequiresAnalysis(string filePath);

        /// <summary>Analyses C# source and returns the extracted symbols as JSON.</summary>
        Task<string> AnalyseSymbolsAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
