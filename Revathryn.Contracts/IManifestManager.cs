namespace Revathryn.Contracts
{
    public interface IManifestManager
    {
        /// <summary>
        /// Returns true if the source file at <paramref name="filePath"/> needs to be
        /// (re-)analysed. Uses a two-phase check: LastWriteTimeUtc as a cheap pre-filter,
        /// then SHA256 of the file contents for certainty. Also returns true when the
        /// stored analyser version is lower than <paramref name="currentAnalyserVersion"/>,
        /// regardless of whether the source has changed.
        /// </summary>
        bool RequiresAnalysis(string filePath, int currentAnalyserVersion);

        /// <summary>
        /// Records a completed analysis in the manifest and persists it to the backing store.
        /// </summary>
        void RecordAnalysis(IManifestEntry entry);

        /// <summary>
        /// Returns the stored entry for <paramref name="filePath"/>, or null if not present.
        /// </summary>
        IManifestEntry GetEntry(string filePath);
    }
}
