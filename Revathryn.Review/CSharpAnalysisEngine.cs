using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Review.CSharp;

namespace Revathryn.Review
{
    /// <summary>Wraps the retained Roslyn-based C# analyser behind the engine contracts.</summary>
    public sealed class CSharpAnalysisEngine : ICSharpAnalysisEngine
    {
        private readonly IAnalyser _analyser;
        private readonly IManifestManager _manifestManager;
        private readonly ISymbolIndexStore _symbolIndexStore;

        /// <summary>Initialises a new engine over the supplied analyser, manifest and optional symbol index store.</summary>
        /// <param name="analyser">Analyser used to extract symbols.</param>
        /// <param name="manifestManager">Manifest manager used to gate and record analysis.</param>
        /// <param name="symbolIndexStore">Optional per-repository symbol index store. Indexing is skipped when null.</param>
        public CSharpAnalysisEngine(IAnalyser analyser, IManifestManager manifestManager, ISymbolIndexStore symbolIndexStore = null)
        {
            _analyser = analyser;
            _manifestManager = manifestManager;
            _symbolIndexStore = symbolIndexStore;
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

        /// <summary>Analyses C# source, returns the extracted symbols as JSON, and refreshes the repository index.</summary>
        public async Task<string> AnalyseSymbolsAsync(string repositoryName, string filePath, CancellationToken cancellationToken = default)
        {
            await AnalyseAsync(repositoryName, filePath, cancellationToken).ConfigureAwait(false);

            IManifestEntry entry = _manifestManager.GetEntry(repositoryName, filePath);

            if (entry is null || string.IsNullOrWhiteSpace(entry.OutputFile) || !File.Exists(entry.OutputFile))
            {
                return string.Empty;
            }

            string json = await File.ReadAllTextAsync(entry.OutputFile, cancellationToken).ConfigureAwait(false);

            await IndexSymbolsAsync(repositoryName, entry, json, cancellationToken).ConfigureAwait(false);

            return json;
        }

        private async Task IndexSymbolsAsync(string repositoryName, IManifestEntry entry, string json, CancellationToken cancellationToken)
        {
            if (_symbolIndexStore is null || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            List<CodeSymbol> symbols = DeserializeSymbols(json);

            if (symbols is null || symbols.Count == 0)
            {
                return;
            }

            IndexedFile file = new()
            {
                SourcePath = entry.SourcePath ?? string.Empty,
                SourceHash = entry.SourceHash ?? string.Empty,
                SourceLastWriteUtc = entry.SourceLastWriteUtc,
                AnalysedUtc = entry.AnalysedUtc,
                AnalyserVersion = entry.AnalyserVersion,
                Language = entry.Language ?? string.Empty,
                LanguageVersion = entry.LanguageVersion ?? string.Empty
            };

            IReadOnlyList<IndexedSymbol> indexedSymbols = [.. symbols.Select(CodeSymbolMapper.ToIndexedSymbol)];

            await _symbolIndexStore.UpsertFileAsync(repositoryName, file, indexedSymbols, cancellationToken).ConfigureAwait(false);
        }

        private static List<CodeSymbol> DeserializeSymbols(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<CodeSymbol>>(json);
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
