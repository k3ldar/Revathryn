using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RevathrynVS2026.Services
{
    /// <summary>Tracks the active document frame through the shell selection service.</summary>
    internal sealed class ActiveDocumentTracker : IActiveDocumentSource, IVsSelectionEvents, IDisposable
    {
        #region Fields
        private readonly IVsMonitorSelection monitorSelection;

        private uint selectionCookie;
        #endregion Fields

        #region Events
        public event EventHandler ActiveFileChanged;
        #endregion Events

        #region Properties
        public string ActiveFilePath { get; private set; }
        #endregion Properties

        #region Constructors
        private ActiveDocumentTracker(IVsMonitorSelection monitorSelection)
        {
            this.monitorSelection = monitorSelection;
        }

        #endregion Constructors

        #region Public Methods
        /// <summary>Creates a tracker and starts listening. Must be called on the UI thread.</summary>
        /// <param name="monitorSelection">The shell selection service.</param>
        /// <returns>The started tracker.</returns>
        public static ActiveDocumentTracker Start(IVsMonitorSelection monitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (monitorSelection == null)
            {
                throw new ArgumentNullException(nameof(monitorSelection));
            }

            ActiveDocumentTracker tracker = new(monitorSelection);
            ErrorHandler.ThrowOnFailure(monitorSelection.AdviseSelectionEvents(tracker, out tracker.selectionCookie));
            ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frame));
            tracker.ActiveFilePath = ResolvePath(frame as IVsWindowFrame);
            return tracker;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (elementid != (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame)
            {
                return VSConstants.S_OK;
            }

            string path = ResolvePath(varValueNew as IVsWindowFrame);

            if (!string.Equals(path, ActiveFilePath, StringComparison.OrdinalIgnoreCase))
            {
                ActiveFilePath = path;
                ActiveFileChanged?.Invoke(this, EventArgs.Empty);
            }

            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.S_OK;
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (selectionCookie != 0)
            {
                monitorSelection.UnadviseSelectionEvents(selectionCookie);
                selectionCookie = 0;
            }
        }

        #endregion Public Methods

        #region Private Methods
        private static string ResolvePath(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (frame == null)
            {
                return null;
            }

            int result = frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object document);
            return ErrorHandler.Succeeded(result) ? document as string : null;
        }

        #endregion Private Methods
    }
}
