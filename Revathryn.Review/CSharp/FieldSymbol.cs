namespace Revathryn.Review.CSharp
{
    public class FieldSymbol
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Accessibility { get; set; } = string.Empty;

        public bool IsStatic { get; set; }

        public bool IsReadOnly { get; set; }
    }
}
