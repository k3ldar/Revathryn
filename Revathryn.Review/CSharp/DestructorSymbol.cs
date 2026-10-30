using System.Collections.Generic;

namespace Revathryn.Review.CSharp
{
    public class DestructorSymbol
    {
        public int LineNumber { get; set; }

        public int EndLineNumber { get; set; }

        public List<string> ReferencedTypes { get; set; } = [];

        public List<InvocationSymbol> Invocations { get; set; } = [];
    }
}
