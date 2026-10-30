using System.Collections.Generic;

namespace Revathryn.Review.CSharp
{
    public class MethodSymbol
    {
        public string Name { get; set; } = string.Empty;

        public string ReturnType { get; set; } = string.Empty;

        public List<ParameterSymbol> Parameters { get; set; } = [];

        public string Accessibility { get; set; } = string.Empty;

        public bool IsAsync { get; set; }

        public bool IsStatic { get; set; }

        public bool IsAbstract { get; set; }

        public bool IsVirtual { get; set; }

        public bool IsOverride { get; set; }

        public bool IsSealed { get; set; }

        public int LineNumber { get; set; }

        public int EndLineNumber { get; set; }

        public List<string> ReferencedTypes { get; set; } = [];

        public List<InvocationSymbol> Invocations { get; set; } = [];
    }
}
