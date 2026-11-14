using System.Net.Http;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using RevathrynVS2026.Commands;
using RevathrynVS2026.Options;
using RevathrynVS2026.Services;
using RevathrynVS2026.ToolWindows;

namespace RevathrynVS2026
{
    /// <summary>Registers the active file pane, its command and its options page.</summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ActiveFileToolWindowPane), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.SolutionExplorer)]
    [ProvideOptionPage(typeof(EndpointOptionsPage), Constants.OptionsCategory, Constants.OptionsPageName, 0, 0, true)]
    public sealed class ActiveFilePanePackage : AsyncPackage
    {
        #region Public Methods
        /// <inheritdoc/>
        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType.Equals(new Guid(PackageGuids.ToolWindowString)) ? this : null;
        }

        #endregion Public Methods

        #region Protected Methods
        /// <inheritdoc/>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await ShowActiveFilePaneCommand.InitializeAsync(this);
            await ConfigureCommand.InitializeAsync(this);
        }

        /// <inheritdoc/>
        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(ActiveFileToolWindowPane) ? Constants.ToolWindowTitle : base.GetToolWindowTitle(toolWindowType, id);
        }

        /// <inheritdoc/>
        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            IVsMonitorSelection monitorSelection = await GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            EndpointOptionsPage options = (EndpointOptionsPage)GetDialogPage(typeof(EndpointOptionsPage));
            HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(Constants.RequestTimeoutSeconds) };
            ActiveDocumentTracker tracker = ActiveDocumentTracker.Start(monitorSelection);
            ActiveFileEndpointClient client = new(httpClient, options);
            ActiveFileViewModel viewModel = new(tracker, client, new EditorNavigator(this), JoinableTaskFactory);
            viewModel.Start();
            return new ActiveFileToolWindowState(viewModel, tracker, httpClient);
        }

        #endregion Protected Methods
    }
}
