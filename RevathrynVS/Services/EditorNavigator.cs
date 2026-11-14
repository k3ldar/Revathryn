using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace RevathrynVS2026.Services
{
    /// <summary>Opens files and selects line ranges through the shell.</summary>
    internal sealed class EditorNavigator : IEditorNavigator
    {
        #region Fields
        private readonly IServiceProvider serviceProvider;
        #endregion Fields

        #region Constructors
        public EditorNavigator(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            this.serviceProvider = serviceProvider;
        }

        #endregion Constructors

        #region Public Methods
        public void GoTo(string filePath, int startLine, int endLine)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(filePath) || startLine < 1)
            {
                return;
            }

            try
            {
                VsShellUtilities.OpenDocument(serviceProvider, filePath, Guid.Empty, out _, out _, out IVsWindowFrame frame);
                ErrorHandler.ThrowOnFailure(frame.Show());
                IVsTextView textView = VsShellUtilities.GetTextView(frame);

                if (textView == null)
                {
                    return;
                }

                int start = startLine - 1;
                int end = Math.Max(startLine, endLine) - 1;
                ErrorHandler.ThrowOnFailure(textView.GetBuffer(out IVsTextLines lines));

                // Lines past the end of the buffer cannot be selected
                if (ErrorHandler.Failed(lines.GetLengthOfLine(end, out int endLength)))
                {
                    return;
                }

                ErrorHandler.ThrowOnFailure(textView.SetSelection(start, 0, end, endLength));
                ErrorHandler.ThrowOnFailure(textView.CenterLines(start, end - start + 1));
            }
            catch (COMException)
            {
                // The file could not be opened or the line no longer exists; leave the editor as it is
            }
        }

        #endregion Public Methods
    }
}
