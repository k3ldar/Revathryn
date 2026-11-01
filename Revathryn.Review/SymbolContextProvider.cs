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

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>Initialises a new context provider.</summary>
        public SymbolContextProvider(IManifestManager manifestManager)
        {
            _manifestManager = manifestManager;
        }

        /// <summary>Returns context for the requested key, e.g. symbol information.</summary>
        public async Task<ReviewContextResponse> GetContextAsync(ReviewContextRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            ReviewContextResponse response = new()
            {
                ContextKey = request.ContextKey ?? string.Empty
            };

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

            return response;
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
                items.Add(new ContextItem
                {
                    Title = ResolveSymbolTitle(symbol),
                    Content = Truncate(JsonSerializer.Serialize(symbol, JsonOptions)),
                    SourcePath = symbol.FilePath ?? string.Empty
                });
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

                items.Add(new ContextItem
                {
                    Title = string.Concat(ResolveSymbolTitle(symbol), Consts.StringSpace, Consts.ContextKeyDependencies),
                    Content = Truncate(string.Join(Consts.CharComma, dependencies)),
                    SourcePath = symbol.FilePath ?? string.Empty
                });

                if (items.Count >= maxItems)
                {
                    break;
                }
            }

            return items;
        }

        private static string ResolveSymbolTitle(CodeSymbol symbol)
        {
            return string.IsNullOrWhiteSpace(symbol.FullyQualifiedName) ? symbol.Name ?? string.Empty : symbol.FullyQualifiedName;
        }

        private static string Truncate(string content)
        {
            if (content is null || content.Length <= Consts.MaxContextItemContentLength)
            {
                return content ?? string.Empty;
            }

            return content[..Consts.MaxContextItemContentLength];
        }
    }
}
