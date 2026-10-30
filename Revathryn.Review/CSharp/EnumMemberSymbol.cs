namespace Revathryn.Review.CSharp
{
    public class EnumMemberSymbol
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Explicit value as written in source, e.g. "1" or "0xFF". Null when not explicitly set.
        /// </summary>
        public string Value { get; set; }
    }
}
