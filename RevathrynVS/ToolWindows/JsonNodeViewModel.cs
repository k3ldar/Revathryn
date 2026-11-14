namespace RevathrynVS2026.ToolWindows
{
    /// <summary>A node displayed in the JSON tree.</summary>
    internal sealed class JsonNodeViewModel
    {
        #region Properties
        public string Name { get; }

        public string Value { get; }

        public IReadOnlyList<JsonNodeViewModel> Children { get; }

        public string DisplayText => Value == null ? Name : $"{Name}: {Value}";
        #endregion Properties

        #region Constructors
        public JsonNodeViewModel(string name, string value, IReadOnlyList<JsonNodeViewModel> children)
        {
            Name = name;
            Value = value;
            Children = children;
        }

        #endregion Constructors
    }
}
