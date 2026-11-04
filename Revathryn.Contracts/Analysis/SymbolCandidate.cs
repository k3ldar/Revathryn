namespace Revathryn.Contracts.Analysis
{
    /// <summary>A single ranked symbol candidate returned by the index query.</summary>
    public sealed class SymbolCandidate
    {
        /// <summary>Fully qualified name of the candidate symbol.</summary>
        public string FullyQualifiedName { get; set; } = string.Empty;

        /// <summary>Simple name of the candidate symbol.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Symbol kind, e.g. "Class" or "Interface".</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Absolute path of the file that defines the candidate symbol.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>First source line of the candidate symbol (1-based).</summary>
        public int LineNumber { get; set; }

        /// <summary>Last source line of the candidate symbol (1-based).</summary>
        public int EndLineNumber { get; set; }

        /// <summary>Human-readable explanation of why the candidate was selected.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Relevance score used for ordering. Higher is more relevant.</summary>
        public int Score { get; set; }

        /// <summary>Pre-formatted content describing the candidate.</summary>
        public string Content { get; set; } = string.Empty;
    }
}
