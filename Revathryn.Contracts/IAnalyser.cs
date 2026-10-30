namespace Revathryn.Contracts
{
    public interface IAnalyser
    {
        /// <summary>Display name of the analyser.</summary>
        string Name { get; }

        /// <summary>File search patterns this analyser handles, e.g. "*.cs", "*.cpp".</summary>
        IReadOnlyList<string> FilePatterns { get; }

        /// <summary>Language this analyser handles, e.g. "CSharp", "TypeScript".</summary>
        string Language { get; }

        /// <summary>Language specification version this analyser targets, e.g. "14.0".</summary>
        string LanguageVersion { get; }

        /// <summary>
        /// Monotonically increasing version of the analyser's extraction logic.
        /// Incrementing this forces all files for this language to be re-analysed
        /// on the next run, even if their source has not changed.
        /// </summary>
        int AnalyserVersion { get; }

        void Analyse(string filePath, IManifestManager manifestManager);
    }
}
