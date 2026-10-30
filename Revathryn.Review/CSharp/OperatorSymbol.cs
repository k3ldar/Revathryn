using System.Collections.Generic;

namespace Revathryn.Review.CSharp
{
    public class OperatorSymbol
    {
        public string OperatorToken { get; set; } = string.Empty;

        public string ReturnType { get; set; } = string.Empty;

        public string Accessibility { get; set; } = string.Empty;

        public bool IsStatic { get; set; }

        public int LineNumber { get; set; }

        public int EndLineNumber { get; set; }

        public List<ParameterSymbol> Parameters { get; set; } = [];

        public List<string> ReferencedTypes { get; set; } = [];

        public List<InvocationSymbol> Invocations { get; set; } = [];
    }
}
