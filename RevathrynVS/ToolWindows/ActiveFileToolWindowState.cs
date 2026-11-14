using System.Net.Http;

using Microsoft.VisualStudio.Shell;

using RevathrynVS2026.Services;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>Owns the objects that live as long as the tool window.</summary>
    public sealed class ActiveFileToolWindowState : IDisposable
    {
        #region Fields
        private readonly ActiveDocumentTracker tracker;

        private readonly HttpClient httpClient;
        #endregion Fields

        #region Properties
        internal ActiveFileViewModel ViewModel { get; }
        #endregion Properties

        #region Constructors
        internal ActiveFileToolWindowState(ActiveFileViewModel viewModel, ActiveDocumentTracker tracker, HttpClient httpClient)
        {
            ViewModel = viewModel;
            this.tracker = tracker;
            this.httpClient = httpClient;
        }

        #endregion Constructors

        #region Public Methods
        /// <summary>Stops tracking the active file and releases the HTTP client.</summary>
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ViewModel.Dispose();
            tracker.Dispose();
            httpClient.Dispose();
        }

        #endregion Public Methods
    }
}
