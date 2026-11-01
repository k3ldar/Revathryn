using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;

namespace Revathryn.Review
{
    /// <summary>Wraps the retained Roslyn-based C# analyser behind the engine contracts.</summary>
    public sealed class CSharpAnalysisEngine : ICSharpAnalysisEngine
    {
        private readonly IAnalyser _analyser;
        private readonly IManifestManager _manifestManager;

        /// <summary>Initialises a new engine over the supplied analyser and manifest.</summary>
        public CSharpAnalysisEngine(IAnalyser analyser, IManifestManager manifestManager)
        {
            _analyser = analyser;
            _manifestManager = manifestManager;
        }

        /// <summary>Display name of the engine.</summary>
        public string Name => _analyser.Name;

        /// <summary>Language handled by the engine.</summary>
        public string Language => _analyser.Language;

        /// <summary>File search patterns the engine handles.</summary>
        public IReadOnlyList<string> FilePatterns => _analyser.FilePatterns;

        /// <summary>Returns true when the file needs (re)analysis according to the repository's manifest.</summary>
        public bool RequiresAnalysis(string repositoryName, string filePath)
        {
            return _manifestManager.RequiresAnalysis(repositoryName, filePath, _analyser.AnalyserVersion);
        }

        /// <summary>Analyses a single source file owned by the named repository.</summary>
        public Task AnalyseAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => _analyser.Analyse(repositoryName, filePath, _manifestManager), cancellationToken);
        }

        /// <summary>Analyses C# source and returns the extracted symbols as JSON.</summary>
        public async Task<string> AnalyseSymbolsAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default)
        {
            await AnalyseAsync(repositoryName, filePath, cancellationToken).ConfigureAwait(false);

            IManifestEntry entry = _manifestManager.GetEntry(repositoryName, filePath);

            if (entry is null || string.IsNullOrWhiteSpace(entry.OutputFile) || !File.Exists(entry.OutputFile))
            {
                return string.Empty;
            }

            return await File.ReadAllTextAsync(entry.OutputFile, cancellationToken).ConfigureAwait(false);
        }
    }
}
