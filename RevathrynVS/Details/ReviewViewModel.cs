namespace RevathrynVS2026.Details
{
    /// <summary>The state of one review type for the active file.</summary>
    internal sealed class ReviewViewModel
    {
        #region Constructors
        public ReviewViewModel(string name, string statusText, string analysedText)
        {
            Name = name;
            StatusText = statusText;
            AnalysedText = analysedText;
        }

        #endregion Constructors

        #region Properties
        public string Name { get; }

        public string StatusText { get; }

        public string AnalysedText { get; }
        #endregion Properties
    }
}
