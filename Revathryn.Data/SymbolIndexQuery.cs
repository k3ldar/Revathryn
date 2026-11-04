using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Data
{
    /// <summary>Answers ranked symbol and file candidate queries against the per-repository index.</summary>
    public sealed class SymbolIndexQuery : ISymbolIndexQuery
    {
        private readonly ISymbolIndexStore _store;

        /// <summary>Initialises a new query service over the supplied store.</summary>
        /// <param name="store">Per-repository symbol index store.</param>
        public SymbolIndexQuery(ISymbolIndexStore store)
        {
            _store = store;
        }

        /// <summary>Returns ranked symbol candidates for the supplied request.</summary>
        public async Task<SymbolQueryResponse> QueryAsync(string repositoryName, SymbolQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            SymbolQueryResponse response = new()
            {
                ContextKey = request.ContextKey ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                return response;
            }

            int maxItems = request.MaxItems > 0 ? request.MaxItems : MaxContextItemsPerRequest;

            if (string.Equals(request.ContextKey, ContextKeySearch, StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<IndexedSymbol> matches = await _store.SearchSymbolsAsync(repositoryName, request.Query, cancellationToken).ConfigureAwait(false);
                response.Candidates = RankSearchResults(matches, request.Query, maxItems);
                return response;
            }

            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                return response;
            }

            IReadOnlyList<IndexedSymbol> fileSymbols = await _store.GetSymbolsForFileAsync(repositoryName, request.SourcePath, cancellationToken).ConfigureAwait(false);

            if (string.Equals(request.ContextKey, ContextKeySymbols, StringComparison.OrdinalIgnoreCase))
            {
                response.Candidates = RankFileSymbols(fileSymbols, maxItems);
            }
            else if (string.Equals(request.ContextKey, ContextKeyDependencies, StringComparison.OrdinalIgnoreCase))
            {
                response.Candidates = await RankDependenciesAsync(repositoryName, fileSymbols, maxItems, cancellationToken).ConfigureAwait(false);
            }
            else if (IsReferenceKey(request.ContextKey))
            {
                response.Candidates = await RankReferencesAsync(repositoryName, request.SourcePath, fileSymbols, request.Query, maxItems, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(request.ContextKey, ContextKeyRelated, StringComparison.OrdinalIgnoreCase))
            {
                response.Candidates = await RankRelatedAsync(repositoryName, request.SourcePath, fileSymbols, maxItems, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }

        private static bool IsReferenceKey(string contextKey)
        {
            return string.Equals(contextKey, ContextKeyFiles, StringComparison.OrdinalIgnoreCase)
                || string.Equals(contextKey, ContextKeyCallers, StringComparison.OrdinalIgnoreCase);
        }

        private static List<SymbolCandidate> RankFileSymbols(IReadOnlyList<IndexedSymbol> symbols, int maxItems)
        {
            return [.. symbols
                .Take(maxItems)
                .Select((symbol, index) => CreateCandidate(symbol, ContextKeySymbols, symbols.Count - index))];
        }

        private async Task<List<SymbolCandidate>> RankDependenciesAsync(
            string repositoryName,
            IReadOnlyList<IndexedSymbol> fileSymbols,
            int maxItems,
            CancellationToken cancellationToken)
        {
            Dictionary<string, int> dependencyCounts = CountDependencies(fileSymbols);

            if (dependencyCounts.Count == 0)
            {
                return [];
            }

            IReadOnlyList<IndexedSymbol> resolved = await _store.GetSymbolsByNamesAsync(repositoryName, dependencyCounts.Keys, cancellationToken).ConfigureAwait(false);

            List<SymbolCandidate> candidates = [];

            foreach (IndexedSymbol symbol in resolved)
            {
                int score = HighestDependencyScore(symbol, dependencyCounts);
                string reason = string.Concat(ContextKeyDependencies, StringSpace, ResolveDisplayName(symbol));
                candidates.Add(CreateCandidate(symbol, reason, score));
            }

            return OrderAndLimit(candidates, maxItems);
        }

        private async Task<List<SymbolCandidate>> RankReferencesAsync(
            string repositoryName,
            string sourcePath,
            IReadOnlyList<IndexedSymbol> fileSymbols,
            string fileNameFilter,
            int maxItems,
            CancellationToken cancellationToken)
        {
            HashSet<string> definitions = BuildDefinitionNames(fileSymbols);

            if (definitions.Count == 0)
            {
                return [];
            }

            IReadOnlyList<IndexedSymbol> referencing = await _store.GetSymbolsReferencingAsync(repositoryName, definitions, cancellationToken).ConfigureAwait(false);

            List<SymbolCandidate> candidates = [];

            foreach (IndexedSymbol symbol in referencing)
            {
                if (PathsReferToSameFile(symbol.FilePath, sourcePath))
                {
                    continue;
                }

                if (!PathMatchesFilter(symbol.FilePath, fileNameFilter))
                {
                    continue;
                }

                string matched = FindFirstMatchedDefinition(symbol, definitions);

                if (string.IsNullOrEmpty(matched))
                {
                    continue;
                }

                candidates.Add(CreateCandidate(symbol, string.Concat(ContextLabelReferences, StringSpace, matched), 100));
            }

            return OrderAndLimit(candidates, maxItems);
        }

        private async Task<List<SymbolCandidate>> RankRelatedAsync(
            string repositoryName,
            string sourcePath,
            IReadOnlyList<IndexedSymbol> fileSymbols,
            int maxItems,
            CancellationToken cancellationToken)
        {
            HashSet<string> definitions = BuildDefinitionNames(fileSymbols);

            if (definitions.Count == 0)
            {
                return [];
            }

            IReadOnlyList<IndexedSymbol> referencing = await _store.GetSymbolsReferencingAsync(repositoryName, definitions, cancellationToken).ConfigureAwait(false);

            List<SymbolCandidate> candidates = [];

            foreach (IndexedSymbol symbol in referencing)
            {
                if (PathsReferToSameFile(symbol.FilePath, sourcePath))
                {
                    continue;
                }

                List<string> matched = FindMatchedDefinitions(symbol, definitions);

                if (matched.Count == 0)
                {
                    continue;
                }

                candidates.Add(CreateRelatedCandidate(symbol, matched));
            }

            return OrderAndLimit(candidates, maxItems);
        }

        private static List<SymbolCandidate> RankSearchResults(IReadOnlyList<IndexedSymbol> symbols, string query, int maxItems)
        {
            string term = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();

            return [.. symbols
                .Take(maxItems)
                .Select((symbol, index) => CreateCandidate(symbol, string.Concat(ContextKeySearch, StringSpace, term).Trim(), symbols.Count - index))];
        }

        private static Dictionary<string, int> CountDependencies(IReadOnlyList<IndexedSymbol> symbols)
        {
            Dictionary<string, int> counts = [with(StringComparer.Ordinal)];

            foreach (IndexedSymbol symbol in symbols)
            {
                foreach (string dependency in symbol.Dependencies ?? [])
                {
                    string cleaned = dependency?.Trim() ?? string.Empty;

                    if (string.IsNullOrEmpty(cleaned))
                    {
                        continue;
                    }

                    counts[cleaned] = counts.TryGetValue(cleaned, out int existing) ? existing + 1 : 1;

                    string lastSegment = LastSegment(cleaned);

                    if (!string.IsNullOrEmpty(lastSegment))
                    {
                        counts[lastSegment] = counts.TryGetValue(lastSegment, out int lastExisting) ? lastExisting + 1 : 1;
                    }
                }
            }

            return counts;
        }

        private static int HighestDependencyScore(IndexedSymbol symbol, Dictionary<string, int> dependencyCounts)
        {
            int score = 0;

            foreach (string name in ResolveNames(symbol))
            {
                if (dependencyCounts.TryGetValue(name, out int count))
                {
                    score = Math.Max(score, count * 10);
                }
            }

            return score;
        }

        private static HashSet<string> BuildDefinitionNames(IReadOnlyList<IndexedSymbol> symbols)
        {
            HashSet<string> names = [with(StringComparer.Ordinal)];

            foreach (IndexedSymbol symbol in symbols)
            {
                AddDefinitionName(names, symbol.Name);
                AddDefinitionName(names, symbol.FullyQualifiedName);
            }

            return names;
        }

        private static void AddDefinitionName(HashSet<string> names, string value)
        {
            string cleaned = value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(cleaned))
            {
                return;
            }

            names.Add(cleaned);

            string lastSegment = LastSegment(cleaned);

            if (!string.IsNullOrEmpty(lastSegment))
            {
                names.Add(lastSegment);
            }
        }

        private static string FindFirstMatchedDefinition(IndexedSymbol symbol, HashSet<string> definitions)
        {
            foreach (string dependency in symbol.Dependencies ?? [])
            {
                if (MatchesDefinition(dependency, definitions))
                {
                    return dependency.Trim();
                }
            }

            return string.Empty;
        }

        private static bool MatchesDefinition(string reference, HashSet<string> definitions)
        {
            string candidate = reference?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            if (definitions.Contains(candidate))
            {
                return true;
            }

            string lastSegment = LastSegment(candidate);
            return !string.IsNullOrEmpty(lastSegment) && definitions.Contains(lastSegment);
        }

        private static List<string> FindMatchedDefinitions(IndexedSymbol symbol, HashSet<string> definitions)
        {
            List<string> matched = [];

            foreach (string dependency in symbol.Dependencies ?? [])
            {
                if (MatchesDefinition(dependency, definitions))
                {
                    string cleaned = dependency?.Trim() ?? string.Empty;

                    if (!string.IsNullOrEmpty(cleaned) && !matched.Contains(cleaned, StringComparer.Ordinal))
                    {
                        matched.Add(cleaned);
                    }
                }
            }

            return matched;
        }

        private static SymbolCandidate CreateRelatedCandidate(IndexedSymbol symbol, List<string> matched)
        {
            return new SymbolCandidate
            {
                FullyQualifiedName = symbol.FullyQualifiedName ?? string.Empty,
                Name = symbol.Name ?? string.Empty,
                Kind = symbol.Kind ?? string.Empty,
                FilePath = symbol.FilePath ?? string.Empty,
                LineNumber = symbol.LineNumber,
                EndLineNumber = symbol.EndLineNumber,
                Reason = ContextKeyRelated,
                Score = 100,
                Content = BuildRelatedContent(symbol, matched)
            };
        }

        private static string BuildRelatedContent(IndexedSymbol symbol, List<string> matched)
        {
            string location = string.Concat(
                Path.GetFileName(symbol.FilePath ?? string.Empty),
                CharColon,
                symbol.LineNumber.ToString());

            return string.Concat(
                string.Join(ListSeparator, matched),
                StringSpace,
                ContextLabelIsUsedBy,
                StringSpace,
                symbol.Kind ?? string.Empty,
                StringSpace,
                ResolveDisplayName(symbol),
                StringSpace,
                CharLeftParenthesis,
                location,
                CharRightParenthesis).Trim();
        }

        private static IEnumerable<string> ResolveNames(IndexedSymbol symbol)
        {
            yield return symbol.Name ?? string.Empty;
            yield return symbol.FullyQualifiedName ?? string.Empty;
        }

        private static SymbolCandidate CreateCandidate(IndexedSymbol symbol, string reason, int score)
        {
            return new SymbolCandidate
            {
                FullyQualifiedName = symbol.FullyQualifiedName ?? string.Empty,
                Name = symbol.Name ?? string.Empty,
                Kind = symbol.Kind ?? string.Empty,
                FilePath = symbol.FilePath ?? string.Empty,
                LineNumber = symbol.LineNumber,
                EndLineNumber = symbol.EndLineNumber,
                Reason = reason ?? string.Empty,
                Score = score,
                Content = BuildContent(symbol)
            };
        }

        private static string BuildContent(IndexedSymbol symbol)
        {
            List<string> lines = [string.Concat(symbol.Kind ?? string.Empty, StringSpace, ResolveDisplayName(symbol)).Trim()];

            foreach (IndexedMember member in symbol.Members ?? [])
            {
                string signature = string.IsNullOrWhiteSpace(member.Signature) ? member.Name ?? string.Empty : member.Signature;

                if (!string.IsNullOrWhiteSpace(signature))
                {
                    lines.Add(signature);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static List<SymbolCandidate> OrderAndLimit(List<SymbolCandidate> candidates, int maxItems)
        {
            return [.. candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.FullyQualifiedName, StringComparer.Ordinal)
                .Take(maxItems)];
        }

        private static string ResolveDisplayName(IndexedSymbol symbol)
        {
            return string.IsNullOrWhiteSpace(symbol.FullyQualifiedName) ? symbol.Name ?? string.Empty : symbol.FullyQualifiedName;
        }

        private static string LastSegment(string value)
        {
            int index = value.LastIndexOf(CharFullStop);
            return index >= 0 ? value[(index + 1)..] : value;
        }

        private static bool PathsReferToSameFile(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathMatchesFilter(string filePath, string filter)
        {
            string target = filePath ?? string.Empty;
            string normalized = (filter ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                return true;
            }

            if (string.Equals(Path.GetFileName(target), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(target, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return target.EndsWith(string.Concat(Path.DirectorySeparatorChar, normalized), StringComparison.OrdinalIgnoreCase)
                || target.EndsWith(string.Concat(Path.AltDirectorySeparatorChar, normalized), StringComparison.OrdinalIgnoreCase);
        }
    }
}
