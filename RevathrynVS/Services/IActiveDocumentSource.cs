namespace RevathrynVS2026.Services
{
    /// <summary>Provides the path of the file that is active in the Visual Studio editor.</summary>
    internal interface IActiveDocumentSource
    {
        #region Events
        /// <summary>Raised on the UI thread when the active file changes.</summary>
        event EventHandler ActiveFileChanged;
        #endregion Events

        #region Properties
        /// <summary>Gets the full path of the active file, or <see langword="null"/> when there is none.</summary>
        string ActiveFilePath { get; }
        #endregion Properties
    }
}
