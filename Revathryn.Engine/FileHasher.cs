using System.Security.Cryptography;

using Revathryn.Contracts.Infrastructure;
using Revathryn.Resources;

namespace Revathryn.Engine
{
    /// <summary>Computes SHA-256 content fingerprints for files on disk.</summary>
    public sealed class FileHasher : IFileHasher
    {
        /// <summary>Computes a SHA-256 fingerprint of the file contents.</summary>
        public string ComputeHash(string filePath)
        {
            FileInfo before = new(filePath);

            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);

            FileInfo after = new(filePath);

            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
            {
                throw new IOException(Strings.FileChangedDuringScan);
            }

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>Asynchronously computes a SHA-256 fingerprint of the file contents.</summary>
        public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo before = new(filePath);

            byte[] hashBytes;
            using (FileStream stream = File.OpenRead(filePath))
            {
                hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            FileInfo after = new(filePath);

            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
            {
                throw new IOException(Strings.FileChangedDuringScan);
            }

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
