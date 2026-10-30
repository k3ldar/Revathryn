namespace Revathryn.Contracts
{
    public interface IManifestEntry
    {
        DateTime AnalysedUtc { get; set; }
        int AnalyserVersion { get; set; }
        string Language { get; set; }
        string LanguageVersion { get; set; }
        string OutputFile { get; set; }
        string SourceHash { get; set; }
        DateTime SourceLastWriteUtc { get; set; }
        string SourcePath { get; set; }
    }
}