using System.Diagnostics;
using System.Net.Http;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RevathrynVS2026.Details;
using RevathrynVS2026.Services;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>Loads endpoint data for the active file and exposes it to the pane.</summary>
    internal sealed class ActiveFileViewModel : ViewModelBase, IDisposable
    {
        #region Fields
        private const string FaultEventName = "RevathrynVS2026/ActiveFile/Load";

        private readonly IActiveDocumentSource documentSource;

        private readonly IActiveFileEndpointClient endpointClient;

        private readonly IEditorNavigator navigator;

        private readonly JoinableTaskFactory joinableTaskFactory;

        private CancellationTokenSource pendingLoad;

        private string filePath;

        private string statusText = ActiveFileMessages.NoActiveFile;

        private bool isBusy;

        private IReadOnlyList<JsonNodeViewModel> nodes = [];

        private FileDetailsViewModel details;
        #endregion Fields

        #region Properties
        public string FilePath
        {
            get => filePath;
            private set => SetProperty(ref filePath, value);
        }

        public string StatusText
        {
            get => statusText;
            private set => SetProperty(ref statusText, value);
        }

        public bool IsBusy
        {
            get => isBusy;
            private set => SetProperty(ref isBusy, value);
        }

        public IReadOnlyList<JsonNodeViewModel> Nodes
        {
            get => nodes;
            private set => SetProperty(ref nodes, value);
        }

        /// <summary>Gets the structured details for the file, or <see langword="null"/> when the response uses another shape.</summary>
        public FileDetailsViewModel Details
        {
            get => details;
            private set
            {
                if (SetProperty(ref details, value))
                {
                    OnPropertyChanged(nameof(HasDetails));
                    OnPropertyChanged(nameof(ShowNodes));
                }
            }
        }

        public bool HasDetails => details != null;

        /// <summary>Gets a value indicating whether the generic JSON tree is shown.</summary>
        public bool ShowNodes => details == null;
        #endregion Properties

        #region Constructors
        public ActiveFileViewModel(
            IActiveDocumentSource documentSource,
            IActiveFileEndpointClient endpointClient,
            IEditorNavigator navigator,
            JoinableTaskFactory joinableTaskFactory)
        {
            if (documentSource == null)
            {
                throw new ArgumentNullException(nameof(documentSource));
            }

            if (endpointClient == null)
            {
                throw new ArgumentNullException(nameof(endpointClient));
            }

            if (joinableTaskFactory == null)
            {
                throw new ArgumentNullException(nameof(joinableTaskFactory));
            }

            if (navigator == null)
            {
                throw new ArgumentNullException(nameof(navigator));
            }

            this.documentSource = documentSource;
            this.endpointClient = endpointClient;
            this.navigator = navigator;
            this.joinableTaskFactory = joinableTaskFactory;
        }

        #endregion Constructors

        #region Public Methods
        /// <summary>Starts following the active file and loads data for the current one.</summary>
        public void Start()
        {
            documentSource.ActiveFileChanged += OnActiveFileChanged;
            Refresh();
        }

        /// <summary>Reloads the data for the active file.</summary>
        public void Refresh()
        {
            string path = documentSource.ActiveFilePath;
            joinableTaskFactory.RunAsync(() => LoadAsync(path)).Task.FileAndForget(FaultEventName);
        }

        public void Dispose()
        {
            documentSource.ActiveFileChanged -= OnActiveFileChanged;
            CancelPendingLoad();
        }

        #endregion Public Methods

        #region Private Methods
        private void OnActiveFileChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        private void ChangeSeverity(long findingId, IssueSeverity severity)
        {
            RunWriteOperation(cancellationToken => endpointClient.UpdateSeverityAsync(findingId, (int)severity, cancellationToken));
        }

        private void ChangeStatus(long findingId, IssueStatus status)
        {
            RunWriteOperation(cancellationToken => endpointClient.UpdateStatusAsync(findingId, (int)status, cancellationToken));
        }

        private void Resubmit(long findingId)
        {
            RunWriteOperation(cancellationToken => endpointClient.ResubmitAsync(findingId, cancellationToken));
        }

        private void ViewOnline(long findingId)
        {
            try
            {
                Uri issueUrl = endpointClient.GetIssueUrl(findingId);
                _ = Process.Start(new ProcessStartInfo(issueUrl.AbsoluteUri) { UseShellExecute = true });
            }
            catch (InvalidOperationException ex)
            {
                StatusText = ex.Message;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                StatusText = string.Format(ActiveFileMessages.RequestFailedFormat, ex.Message);
            }
        }

        private void RunWriteOperation(Func<CancellationToken, Task> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            joinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await operation(CancellationToken.None).ConfigureAwait(true);
                    Refresh();
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellations from disposed views
                }
                catch (InvalidOperationException ex)
                {
                    StatusText = ex.Message;
                }
                catch (HttpRequestException ex)
                {
                    StatusText = string.Format(ActiveFileMessages.RequestFailedFormat, ex.Message);
                }
                catch (JsonException ex)
                {
                    StatusText = string.Format(ActiveFileMessages.InvalidJsonFormat, ex.Message);
                }
            }).Task.FileAndForget(FaultEventName);
        }

        private async Task LoadAsync(string path)
        {
            CancelPendingLoad();
            FilePath = path;

            if (string.IsNullOrEmpty(path))
            {
                IsBusy = false;
                Show([], null, ActiveFileMessages.NoActiveFile);
                return;
            }

            CancellationTokenSource load = new();
            pendingLoad = load;
            IsBusy = true;
            StatusText = ActiveFileMessages.Loading;
            try
            {
                JToken result = await endpointClient.GetAsync(path, load.Token).ConfigureAwait(true);

                if (load.IsCancellationRequested)
                {
                    return;
                }

                FileDetailsViewModel loadedDetails = FileDetailsFactory.TryCreate(
                    result,
                    (start, end) => navigator.GoTo(path, start, end),
                    ChangeSeverity,
                    ChangeStatus,
                    Resubmit,
                    ViewOnline);

                if (loadedDetails != null)
                {
                    Show([], loadedDetails, ActiveFileMessages.Loaded);
                    return;
                }

                IReadOnlyList<JsonNodeViewModel> loaded = JsonNodeFactory.Create(result);
                Show(loaded, null, loaded.Count == 0 ? ActiveFileMessages.NoData : ActiveFileMessages.Loaded);
            }
            catch (OperationCanceledException) when (load.IsCancellationRequested)
            {
                // Superseded by a newer request
            }
            catch (OperationCanceledException)
            {
                Show([], null, ActiveFileMessages.RequestTimedOut);
            }
            catch (InvalidOperationException ex)
            {
                Show([], null, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                Show([], null, string.Format(ActiveFileMessages.RequestFailedFormat, ex.Message));
            }
            catch (JsonException ex)
            {
                Show([], null, string.Format(ActiveFileMessages.InvalidJsonFormat, ex.Message));
            }
            finally
            {
                if (ReferenceEquals(pendingLoad, load))
                {
                    IsBusy = false;
                    pendingLoad = null;
                }

                load.Dispose();
            }
        }

        private void Show(IReadOnlyList<JsonNodeViewModel> newNodes, FileDetailsViewModel newDetails, string status)
        {
            Nodes = newNodes;
            Details = newDetails;
            StatusText = status;
        }

        private void CancelPendingLoad()
        {
            CancellationTokenSource load = pendingLoad;
            pendingLoad = null;
            load?.Cancel();
        }

        #endregion Private Methods
    }
}
