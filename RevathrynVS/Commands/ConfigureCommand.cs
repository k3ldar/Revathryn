using System.ComponentModel.Design;

using Microsoft.VisualStudio.Shell;

using RevathrynVS2026.Configuration;
using RevathrynVS2026.Options;

namespace RevathrynVS2026.Commands
{
    /// <summary>Extensions &gt; Revathryn &gt; Configure command that opens the configure window.</summary>
    internal static class ConfigureCommand
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

            CommandID commandId = new(new Guid(PackageGuids.CommandSetString), Constants.ConfigureCommandId);
            commandService.AddCommand(new MenuCommand((sender, e) => Execute(package), commandId));
        }

        #endregion Public Methods

        #region Private Methods
        private static void Execute(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EndpointOptionsPage options = (EndpointOptionsPage)package.GetDialogPage(typeof(EndpointOptionsPage));
            ConfigureViewModel viewModel = new(options.EndpointUrl, options.Repository);
            ConfigureWindow window = new(viewModel);

            if (window.ShowModal() == true)
            {
                options.EndpointUrl = viewModel.EndpointUrl;
                options.Repository = viewModel.Repository;
                options.SaveSettingsToStorage();
            }
        }

        #endregion Private Methods
    }
}
