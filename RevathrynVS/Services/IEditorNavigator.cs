namespace RevathrynVS2026.Services
{
    /// <summary>Moves the Visual Studio editor to a location in a file.</summary>
    internal interface IEditorNavigator
    {
        #region Public Methods
        /// <summary>Opens <paramref name="filePath"/> and selects the given lines. Must be called on the UI thread.</summary>
        /// <param name="filePath">The full path of the file.</param>
        /// <param name="startLine">The first line to select, 1-based.</param>
        /// <param name="endLine">The last line to select, 1-based.</param>
        void GoTo(string filePath, int startLine, int endLine);
        #endregion Public Methods
    }
}
