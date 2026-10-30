namespace Revathryn.Review.CSharp
{
    public class PropertySymbol
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Accessibility { get; set; } = string.Empty;

        public bool IsStatic { get; set; }
    }
}
