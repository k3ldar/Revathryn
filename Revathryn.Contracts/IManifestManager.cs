namespace Revathryn.Contracts
{
    public interface IManifestManager
    {
        /// <summary>
        /// Returns true if the source file at <paramref name="filePath"/> needs to be
        /// (re-)analysed for the repository named <paramref name="repositoryName"/>.
        /// Uses a two-phase check: LastWriteTimeUtc as a cheap pre-filter,
        /// then SHA256 of the file contents for certainty. Also returns true when the
        /// stored analyser version is lower than <paramref name="currentAnalyserVersion"/>,
        /// regardless of whether the source has changed.
        /// </summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="filePath">Absolute path of the source file.</param>
        /// <param name="currentAnalyserVersion">Current analyser version.</param>
        bool RequiresAnalysis(string repositoryName, string filePath, int currentAnalyserVersion);

        /// <summary>
        /// Records a completed analysis in the named repository's manifest and persists it to the backing store.
        /// </summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="entry">The completed analysis entry to record.</param>
        void RecordAnalysis(string repositoryName, IManifestEntry entry);

        /// <summary>
        /// Returns the stored entry for <paramref name="filePath"/> in the repository named
        /// <paramref name="repositoryName"/>, or null if not present.
        /// </summary>
        /// <param name="repositoryName">Name of the repository that owns the file.</param>
        /// <param name="filePath">Absolute path of the source file.</param>
        IManifestEntry GetEntry(string repositoryName, string filePath);

        /// <summary>
        /// Returns all stored entries for the repository named <paramref name="repositoryName"/>.
        /// </summary>
        /// <param name="repositoryName">Name of the repository whose entries are returned.</param>
        IReadOnlyList<IManifestEntry> GetEntries(string repositoryName);
    }
}
