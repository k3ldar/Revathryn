using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Persistence
{
    /// <summary>Stores and reads per-repository C# symbol index data.</summary>
    public interface ISymbolIndexStore
    {
        /// <summary>Replaces the indexed symbols for a single source file in the repository's index.</summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="file">File state recorded alongside the symbols.</param>
        /// <param name="symbols">Symbols extracted from the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpsertFileAsync(string repositoryName, IndexedFile file, IReadOnlyList<IndexedSymbol> symbols, CancellationToken cancellationToken = default);

        /// <summary>Removes a single source file and its symbols from the repository's index.</summary>
        Task DeleteFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default);

        /// <summary>Removes every indexed file and symbol for the repository.</summary>
        Task DeleteRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default);

        /// <summary>Returns symbols defined in the given source file, including members and dependencies.</summary>
        Task<IReadOnlyList<IndexedSymbol>> GetSymbolsForFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default);

        /// <summary>Returns indexed symbols whose name or fully qualified name matches any of the supplied names.</summary>
        Task<IReadOnlyList<IndexedSymbol>> GetSymbolsByNamesAsync(string repositoryName, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);

        /// <summary>Returns symbols that depend on any of the supplied target names.</summary>
        Task<IReadOnlyList<IndexedSymbol>> GetSymbolsReferencingAsync(string repositoryName, IReadOnlyCollection<string> targetNames, CancellationToken cancellationToken = default);

        /// <summary>Returns symbols whose name, fully qualified name or namespace contains the search text.</summary>
        Task<IReadOnlyList<IndexedSymbol>> SearchSymbolsAsync(string repositoryName, string searchText, CancellationToken cancellationToken = default);
    }
}
