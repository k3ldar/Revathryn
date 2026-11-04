using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;
using Revathryn.Review.CSharp;

namespace Revathryn.Review
{
    /// <summary>
    /// Supplies symbol and dependency context by reading the C# analyser's
    /// per-file symbol JSON through the manifest manager.
    /// </summary>
    public sealed class SymbolContextProvider : IReviewContextProvider
    {
        private readonly IManifestManager _manifestManager;
        private readonly ISymbolIndexQuery _symbolIndexQuery;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>Initialises a new context provider.</summary>
        /// <param name="manifestManager">Manifest manager used as a JSON fallback source.</param>
        /// <param name="symbolIndexQuery">Optional per-repository symbol index query. The JSON source is used when null or empty.</param>
        public SymbolContextProvider(IManifestManager manifestManager, ISymbolIndexQuery symbolIndexQuery = null)
        {
            _manifestManager = manifestManager;
            _symbolIndexQuery = symbolIndexQuery;
        }

        /// <summary>Returns context for the requested key, e.g. symbol information.</summary>
        public async Task<ReviewContextResponse> GetContextAsync(ReviewContextRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ReviewContextResponse response = new()
            {
                ContextKey = request.ContextKey ?? string.Empty
            };

            if (_symbolIndexQuery is not null)
            {
                SymbolQueryResponse queryResponse = await _symbolIndexQuery
                    .QueryAsync(request.RepositoryName, BuildQueryRequest(request), cancellationToken)
                    .ConfigureAwait(false);

                if (queryResponse.Candidates is not null && queryResponse.Candidates.Count > 0)
                {
                    response.Items = BuildContextItems(queryResponse.Candidates);
                    return response;
                }
            }

            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                return response;
            }

            IManifestEntry entry = _manifestManager.GetEntry(request.RepositoryName, request.SourcePath);

            if (entry is null || string.IsNullOrWhiteSpace(entry.OutputFile) || !File.Exists(entry.OutputFile))
            {
                return response;
            }

            List<CodeSymbol> symbols = await ReadSymbolsAsync(entry.OutputFile, cancellationToken).ConfigureAwait(false);

            if (symbols is null || symbols.Count == 0)
            {
                return response;
            }

            int maxItems = request.MaxItems > 0 ? request.MaxItems : Consts.MaxContextItemsPerRequest;

            if (string.Equals(request.ContextKey, Consts.ContextKeySymbols, StringComparison.OrdinalIgnoreCase))
            {
                response.Items = BuildSymbolItems(symbols, maxItems);
            }
            else if (string.Equals(request.ContextKey, Consts.ContextKeyDependencies, StringComparison.OrdinalIgnoreCase))
            {
                response.Items = BuildDependencyItems(symbols, maxItems);
            }
            else if (string.Equals(request.ContextKey, Consts.ContextKeyFiles, StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.ContextKey, Consts.ContextKeyCallers, StringComparison.OrdinalIgnoreCase))
            {
                response.Items = await BuildFileUsageItemsAsync(request.RepositoryName, request.SourcePath, symbols, request.Query, maxItems, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }

        private static SymbolQueryRequest BuildQueryRequest(ReviewContextRequest request)
        {
            return new SymbolQueryRequest
            {
                ContextKey = request.ContextKey ?? string.Empty,
                SourcePath = request.SourcePath ?? string.Empty,
                Query = request.Query ?? string.Empty,
                MaxItems = request.MaxItems
            };
        }

        private static List<ContextItem> BuildContextItems(IReadOnlyList<SymbolCandidate> candidates)
        {
            List<ContextItem> items = [];

            foreach (SymbolCandidate candidate in candidates)
            {
                string title = string.IsNullOrWhiteSpace(candidate.FullyQualifiedName) ? candidate.Name ?? string.Empty : candidate.FullyQualifiedName;
                items.Add(CreateItem(title, candidate.Content, candidate.FilePath));
            }

            return items;
        }

        private static async Task<List<CodeSymbol>> ReadSymbolsAsync(string outputFile, CancellationToken cancellationToken)
        {
            try
            {
                string json = await File.ReadAllTextAsync(outputFile, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<CodeSymbol>>(json);
            }
            catch (JsonException)
            {
                return [];
            }
            catch (IOException)
            {
                return [];
            }
        }

        private static List<ContextItem> BuildSymbolItems(List<CodeSymbol> symbols, int maxItems)
        {
            List<ContextItem> items = [];

            foreach (CodeSymbol symbol in symbols.Take(maxItems))
            {
                items.Add(CreateItem(ResolveSymbolTitle(symbol), JsonSerializer.Serialize(symbol, JsonOptions), symbol.FilePath));
            }

            return items;
        }

        private static List<ContextItem> BuildDependencyItems(List<CodeSymbol> symbols, int maxItems)
        {
            List<ContextItem> items = [];

            foreach (CodeSymbol symbol in symbols)
            {
                List<string> dependencies = symbol.Dependencies?.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToList() ?? [];

                if (dependencies.Count == 0)
                {
                    continue;
                }

                items.Add(CreateItem(
                    string.Concat(ResolveSymbolTitle(symbol), Consts.StringSpace, Consts.ContextKeyDependencies),
                    string.Join(Consts.CharComma, dependencies),
                    symbol.FilePath));

                if (items.Count >= maxItems)
                {
                    break;
                }
            }

            return items;
        }

        private async Task<List<ContextItem>> BuildFileUsageItemsAsync(
            string repositoryName,
            string sourcePath,
            List<CodeSymbol> reviewedSymbols,
            string fileNameFilter,
            int maxItems,
            CancellationToken cancellationToken)
        {
            HashSet<string> definitions = BuildDefinitionNames(reviewedSymbols);

            if (definitions.Count == 0)
            {
                return [];
            }

            IReadOnlyList<IManifestEntry> entries = _manifestManager.GetEntries(repositoryName);

            if (entries is null || entries.Count == 0)
            {
                return [];
            }

            List<ContextItem> items = [];

            foreach (IManifestEntry entry in entries)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.OutputFile) || !File.Exists(entry.OutputFile))
                {
                    continue;
                }

                if (PathsReferToSameFile(entry.SourcePath, sourcePath))
                {
                    continue;
                }

                if (!PathMatchesFilter(entry.SourcePath, fileNameFilter))
                {
                    continue;
                }

                List<CodeSymbol> fileSymbols = await ReadSymbolsAsync(entry.OutputFile, cancellationToken).ConfigureAwait(false);

                if (fileSymbols is null || fileSymbols.Count == 0)
                {
                    continue;
                }

                foreach (CodeSymbol referencingSymbol in fileSymbols)
                {
                    List<string> matchedReferences = FindMatchedReferences(referencingSymbol, definitions);

                    if (matchedReferences.Count == 0)
                    {
                        continue;
                    }

                    items.Add(CreateItem(
                        ResolveSymbolTitle(referencingSymbol),
                        BuildUsageContent(referencingSymbol, matchedReferences),
                        entry.SourcePath ?? string.Empty));

                    if (items.Count >= maxItems)
                    {
                        return items;
                    }
                }
            }

            return items;
        }

        private static HashSet<string> BuildDefinitionNames(List<CodeSymbol> symbols)
        {
            HashSet<string> names = [with(StringComparer.Ordinal)];

            foreach (CodeSymbol symbol in symbols)
            {
                AddDefinitionName(names, symbol.Name);
                AddDefinitionName(names, symbol.FullyQualifiedName);
            }

            return names;
        }

        private static void AddDefinitionName(HashSet<string> names, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            names.Add(trimmed);

            string lastSegment = LastSegment(trimmed);

            if (!string.IsNullOrEmpty(lastSegment))
            {
                names.Add(lastSegment);
            }
        }

        private static List<string> FindMatchedReferences(CodeSymbol symbol, HashSet<string> definitions)
        {
            List<string> matched = [];

            foreach (string dependency in symbol.Dependencies ?? [])
            {
                if (MatchesDefinition(dependency, definitions))
                {
                    AddDistinct(matched, dependency);
                }
            }

            foreach (string baseType in symbol.BaseTypes ?? [])
            {
                foreach (string name in ExtractTypeNames(baseType))
                {
                    if (MatchesDefinition(name, definitions))
                    {
                        AddDistinct(matched, name);
                    }
                }
            }

            foreach (string implementedInterface in symbol.ImplementedInterfaces ?? [])
            {
                foreach (string name in ExtractTypeNames(implementedInterface))
                {
                    if (MatchesDefinition(name, definitions))
                    {
                        AddDistinct(matched, name);
                    }
                }
            }

            return matched;
        }

        private static bool MatchesDefinition(string reference, HashSet<string> definitions)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return false;
            }

            string candidate = reference.Trim();

            if (definitions.Contains(candidate))
            {
                return true;
            }

            string lastSegment = LastSegment(candidate);
            return !string.IsNullOrEmpty(lastSegment) && definitions.Contains(lastSegment);
        }

        private static string LastSegment(string value)
        {
            int index = value.LastIndexOf(Consts.CharFullStop);
            return index >= 0 ? value[(index + 1)..] : value;
        }

        private static IEnumerable<string> ExtractTypeNames(string typeExpression)
        {
            if (string.IsNullOrWhiteSpace(typeExpression))
            {
                yield break;
            }

            string cleaned = typeExpression
                .Replace(Consts.StringQuestionMark, string.Empty)
                .Replace(Consts.StringArrayIdentifier, string.Empty)
                .Trim();

            int lessThan = cleaned.IndexOf(Consts.CharLessThan);

            if (lessThan < 0)
            {
                yield return cleaned;
                yield break;
            }

            int greaterThan = cleaned.LastIndexOf(Consts.CharGreaterThan);

            if (lessThan > 0)
            {
                yield return cleaned[..lessThan].Trim();
            }

            if (greaterThan > lessThan)
            {
                string inner = cleaned[(lessThan + 1)..greaterThan];

                foreach (string part in inner.Split(Consts.CharComma))
                {
                    string trimmedPart = part.Trim().Replace(Consts.StringQuestionMark, string.Empty);

                    if (!string.IsNullOrEmpty(trimmedPart))
                    {
                        yield return trimmedPart;
                    }
                }
            }
        }

        private static string BuildUsageContent(CodeSymbol referencingSymbol, List<string> matchedReferences)
        {
            return string.Concat(
                referencingSymbol.Kind.ToString(),
                Consts.StringSpace,
                Consts.ContextLabelReferences,
                Consts.StringSpace,
                string.Join(Consts.ListSeparator, matchedReferences));
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

        private static void AddDistinct(List<string> list, string value)
        {
            string cleaned = value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(cleaned) || list.Contains(cleaned, StringComparer.Ordinal))
            {
                return;
            }

            list.Add(cleaned);
        }

        private static string ResolveSymbolTitle(CodeSymbol symbol)
        {
            return string.IsNullOrWhiteSpace(symbol.FullyQualifiedName) ? symbol.Name ?? string.Empty : symbol.FullyQualifiedName;
        }

        private static ContextItem CreateItem(string title, string content, string sourcePath)
        {
            string full = content ?? string.Empty;
            bool truncated = full.Length > Consts.MaxContextItemContentLength;

            return new ContextItem
            {
                Title = title,
                Content = truncated ? full[..Consts.MaxContextItemContentLength] : full,
                SourcePath = sourcePath ?? string.Empty,
                Truncated = truncated,
                OriginalLength = truncated ? full.Length : 0
            };
        }
    }
}
