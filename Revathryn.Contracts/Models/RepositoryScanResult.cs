namespace Revathryn.Contracts.Models
{
    /// <summary>Outcome of a single filesystem scan of a repository folder.</summary>
    public class RepositoryScanResult
    {
        /// <summary>Files that were discovered and successfully fingerprinted.</summary>
        public IReadOnlyList<RepoFile> DiscoveredFiles { get; set; } = new List<RepoFile>();

        /// <summary>Files that existed but could not be read or fingerprinted.</summary>
        public IReadOnlyList<ScanFailure> Failures { get; set; } = new List<ScanFailure>();
    }
}
