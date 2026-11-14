namespace RevathrynVS2026.Details
{
    /// <summary>A selectable value shown in an issue action control.</summary>
    internal sealed class IssueOption
    {
        #region Constructors
        public IssueOption(string display, object value)
        {
            Display = display;
            Value = value;
        }
        #endregion Constructors

        #region Properties
        /// <summary>Gets the text shown to the user.</summary>
        public string Display { get; }

        /// <summary>Gets the value sent to the endpoint.</summary>
        public object Value { get; }
        #endregion Properties
    }
}
