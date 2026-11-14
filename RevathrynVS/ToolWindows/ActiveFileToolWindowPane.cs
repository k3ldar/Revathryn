using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>The single pane that shows endpoint data for the active file.</summary>
    [Guid(PackageGuids.ToolWindowString)]
    public sealed class ActiveFileToolWindowPane : ToolWindowPane
    {
        #region Fields
        private readonly ActiveFileToolWindowState state;
        #endregion Fields

        #region Constructors
        /// <summary>Initializes a new instance of the <see cref="ActiveFileToolWindowPane"/> class.</summary>
        /// <param name="state">The state created by the package.</param>
        public ActiveFileToolWindowPane(ActiveFileToolWindowState state)
            : base(null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            this.state = state;
            Caption = Constants.ToolWindowTitle;
            Content = new ActiveFileControl(state.ViewModel);
        }

        #endregion Constructors

        #region Protected Methods
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (disposing)
            {
                state.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion Protected Methods
    }
}
