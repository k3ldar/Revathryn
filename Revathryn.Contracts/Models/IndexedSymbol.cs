namespace Revathryn.Contracts.Models
{
    /// <summary>A top-level source symbol indexed from a C# file.</summary>
    public sealed class IndexedSymbol
    {
        /// <summary>Simple name of the symbol.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Fully qualified name of the symbol.</summary>
        public string FullyQualifiedName { get; set; } = string.Empty;

        /// <summary>Symbol kind, e.g. "Class", "Interface" or "Record".</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Absolute path of the file that defines the symbol.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>First source line of the symbol (1-based).</summary>
        public int LineNumber { get; set; }

        /// <summary>Last source line of the symbol (1-based).</summary>
        public int EndLineNumber { get; set; }

        /// <summary>Namespace containing the symbol.</summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>Accessibility of the symbol.</summary>
        public string Accessibility { get; set; } = string.Empty;

        /// <summary>True when the symbol is static.</summary>
        public bool IsStatic { get; set; }

        /// <summary>True when the symbol is abstract.</summary>
        public bool IsAbstract { get; set; }

        /// <summary>True when the symbol is sealed.</summary>
        public bool IsSealed { get; set; }

        /// <summary>User-defined type names the symbol depends on.</summary>
        public List<string> Dependencies { get; set; } = [];

        /// <summary>Namespace imports (using directives) of the file that defines the symbol.</summary>
        public List<string> UsingDirectives { get; set; } = [];

        /// <summary>Members declared by the symbol.</summary>
        public List<IndexedMember> Members { get; set; } = [];
    }
}
