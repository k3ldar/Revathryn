using System.ComponentModel.Design;

using Microsoft.VisualStudio.Shell;

using RevathrynVS2026.ToolWindows;

namespace RevathrynVS2026.Commands
{
    /// <summary>Extensions &gt; Revathryn &gt; View Insights command that opens the active file pane.</summary>
    internal static class ShowActiveFilePaneCommand
    {
        #region Public Methods
        public static async Task InitializeAsync(AsyncPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService)
            {
                return;
            }

            CommandID commandId = new(new Guid(PackageGuids.CommandSetString), Constants.ShowPaneCommandId);
            commandService.AddCommand(new MenuCommand((sender, e) => Execute(package), commandId));
        }

        #endregion Public Methods

        #region Private Methods
        private static void Execute(AsyncPackage package)
        {
            package.JoinableTaskFactory
                .RunAsync(() => package.ShowToolWindowAsync(typeof(ActiveFileToolWindowPane), 0, true, package.DisposalToken))
                .Task
                .FileAndForget(Constants.ShowPaneFaultEventName);
        }

        #endregion Private Methods
    }
}
