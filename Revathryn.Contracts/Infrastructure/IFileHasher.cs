namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Computes content fingerprints for files on disk.</summary>
    public interface IFileHasher
    {
        /// <summary>Computes a SHA-256 fingerprint of the file contents.</summary>
        /// <param name="filePath">Absolute path of the file to hash.</param>
        /// <returns>Lower-case hexadecimal SHA-256 digest.</returns>
        string ComputeHash(string filePath);

        /// <summary>Asynchronously computes a SHA-256 fingerprint of the file contents.</summary>
        /// <param name="filePath">Absolute path of the file to hash.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Lower-case hexadecimal SHA-256 digest.</returns>
        Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
