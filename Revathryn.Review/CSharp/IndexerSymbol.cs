using System.Collections.Generic;

namespace Revathryn.Review.CSharp
{
    public class IndexerSymbol
    {
        public string Type { get; set; } = string.Empty;

        public string Accessibility { get; set; } = string.Empty;

        public bool HasGetter { get; set; }

        public bool HasSetter { get; set; }

        public int LineNumber { get; set; }

        public int EndLineNumber { get; set; }

        public List<ParameterSymbol> Parameters { get; set; } = [];
    }
}
