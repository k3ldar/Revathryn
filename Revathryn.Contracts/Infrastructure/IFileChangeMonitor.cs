namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Raised when a file change is detected by <see cref="IFileChangeMonitor"/>.</summary>
    public class FileChangedEventArgs : EventArgs
    {
        /// <summary>Full path of the changed file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the change was detected.</summary>
        public DateTime DetectedUtc { get; set; }
    }

    /// <summary>Watches a folder tree and reports file changes.</summary>
    public interface IFileChangeMonitor : IDisposable
    {
        /// <summary>Raised when a file changes under the watched path.</summary>
        event EventHandler<FileChangedEventArgs> FileChanged;

        /// <summary>Starts watching the given path.</summary>
        void Start(string path);

        /// <summary>Stops watching.</summary>
        void Stop();
    }
}
