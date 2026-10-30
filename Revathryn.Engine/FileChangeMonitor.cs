using Revathryn.Contracts.Infrastructure;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>Watches a folder tree and reports file changes.</summary>
    public sealed class FileChangeMonitor : IFileChangeMonitor
    {
        private readonly object _sync = new();
        private FileSystemWatcher _watcher;

        /// <summary>Raised when a file changes under the watched path.</summary>
        public event EventHandler<FileChangedEventArgs> FileChanged;

        /// <summary>Starts watching the given path.</summary>
        public void Start(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(Strings.PathRequired, nameof(path));
            }

            Stop();

            lock (_sync)
            {
                _watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = false
                };

                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnRenamed;
                _watcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>Stops watching.</summary>
        public void Stop()
        {
            lock (_sync)
            {
                if (_watcher is null)
                {
                    return;
                }

                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnChanged;
                _watcher.Created -= OnChanged;
                _watcher.Deleted -= OnChanged;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        /// <summary>Stops watching and releases resources.</summary>
        public void Dispose()
        {
            Stop();
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Raise(e.FullPath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Raise(e.FullPath);
        }

        private void Raise(string filePath)
        {
            FileChanged?.Invoke(this, new FileChangedEventArgs
            {
                FilePath = filePath,
                DetectedUtc = DateTime.UtcNow
            });
        }
    }
}
