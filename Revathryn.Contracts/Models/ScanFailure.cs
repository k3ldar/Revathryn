namespace Revathryn.Contracts.Models
{
    /// <summary>A file that could not be fingerprinted during a scan.</summary>
    public class ScanFailure
    {
        /// <summary>Repository-relative path of the file that failed.</summary>
        public string RelativePath { get; set; } = String.Empty;

        /// <summary>Description of why the file could not be read or hashed.</summary>
        public string Error { get; set; } = String.Empty;
    }
}
