using Revathryn.Contracts;

namespace Revathryn.Engine
{
    public class Manifest
    {
        /// <summary>
        /// Incremented when the manifest schema itself changes in a breaking way.
        /// Allows future readers to detect and handle older manifest formats.
        /// </summary>
        public int ManifestVersion { get; set; } = 1;

        /// <summary>
        /// All indexed source files, keyed by their full source path.
        /// Designed to map directly to a database table row keyed on SourcePath.
        /// </summary>
        public Dictionary<string, IManifestEntry> Entries { get; set; } = [];
    }
}
