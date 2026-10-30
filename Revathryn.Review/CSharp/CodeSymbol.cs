namespace Revathryn.Review.CSharp
{
    public class CodeSymbol
    {
        public string Name { get; set; } = string.Empty;

        public string FullyQualifiedName { get; set; } = string.Empty;

        public SymbolKind Kind { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public int EndLineNumber { get; set; }

        public string Namespace { get; set; } = string.Empty;

        public string Accessibility { get; set; } = string.Empty;

        public bool IsStatic { get; set; }

        public bool IsAbstract { get; set; }

        public bool IsSealed { get; set; }

        public List<string> UsingDirectives { get; set; } = [];

        public List<string> BaseTypes { get; set; } = [];

        public List<string> ImplementedInterfaces { get; set; } = [];

        public List<ParameterSymbol> DelegateParameters { get; set; } = [];

        public string DelegateReturnType { get; set; } = string.Empty;

        public List<ConstructorSymbol> Constructors { get; set; } = [];

        public List<DestructorSymbol> Destructors { get; set; } = [];

        public List<MethodSymbol> Methods { get; set; } = [];

        public List<PropertySymbol> Properties { get; set; } = [];

        public List<FieldSymbol> Fields { get; set; } = [];

        public List<IndexerSymbol> Indexers { get; set; } = [];

        public List<OperatorSymbol> Operators { get; set; } = [];

        public List<EnumMemberSymbol> EnumMembers { get; set; } = [];

        public List<string> Dependencies { get; set; } = [];
    }
}
