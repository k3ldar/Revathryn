namespace Revathryn.Contracts.Analysis
{
    /// <summary>Base contract for an analysis engine that extracts information from source files.</summary>
    public interface IAnalysisEngine
    {
        /// <summary>Display name of the engine.</summary>
        string Name { get; }

        /// <summary>Language handled by the engine.</summary>
        string Language { get; }

        /// <summary>File search patterns the engine handles.</summary>
        IReadOnlyList<string> FilePatterns { get; }

        /// <summary>Analyses a single source file owned by the named repository.</summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="filePath">Absolute path of the source file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AnalyseAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default);
    }
}
