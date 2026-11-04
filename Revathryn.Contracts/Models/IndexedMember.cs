namespace Revathryn.Contracts.Models
{
    /// <summary>A member (method, property, field, and so on) of an indexed symbol.</summary>
    public sealed class IndexedMember
    {
        /// <summary>Member kind, e.g. "Method", "Property" or "Field".</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Member name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Display signature, e.g. "int Add(int a, int b)".</summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>First source line of the member (1-based).</summary>
        public int LineNumber { get; set; }

        /// <summary>Last source line of the member (1-based).</summary>
        public int EndLineNumber { get; set; }

        /// <summary>Accessibility of the member.</summary>
        public string Accessibility { get; set; } = string.Empty;

        /// <summary>True when the member is static.</summary>
        public bool IsStatic { get; set; }
    }
}
