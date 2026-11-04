namespace Revathryn.Contracts.Analysis
{
    /// <summary>Describes a symbol candidate query against a repository index.</summary>
    public sealed class SymbolQueryRequest
    {
        /// <summary>Kind of context requested, e.g. "symbols", "dependencies", "files", "callers", "related" or "search".</summary>
        public string ContextKey { get; set; } = string.Empty;

        /// <summary>Absolute path of the file the query is anchored to.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Free-text query used when <see cref="ContextKey"/> is "search".</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>Maximum number of candidates to return. Zero means the configured default.</summary>
        public int MaxItems { get; set; }
    }
}
