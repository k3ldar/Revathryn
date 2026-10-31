using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Data
{
    /// <summary>
    /// Stores and retrieves secrets as process environment variables using the
    /// secret key as the environment variable name.
    /// </summary>
    public sealed class EnvironmentSecretsProvider : ISecretsProvider
    {
        /// <summary>Returns the secret stored under the given key, or null when absent.</summary>
        public Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Environment.GetEnvironmentVariable(key));
        }

        /// <summary>Stores a secret under the given key.</summary>
        public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);
            cancellationToken.ThrowIfCancellationRequested();

            Environment.SetEnvironmentVariable(key, value);
            return Task.CompletedTask;
        }

        /// <summary>Deletes the secret stored under the given key.</summary>
        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(key);
            cancellationToken.ThrowIfCancellationRequested();

            Environment.SetEnvironmentVariable(key, null);
            return Task.CompletedTask;
        }
    }
}
