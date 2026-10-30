namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Provides secure storage and retrieval of secrets such as AI API keys.</summary>
    public interface ISecretsProvider
    {
        /// <summary>Returns the secret stored under the given key, or null when absent.</summary>
        Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Stores a secret under the given key.</summary>
        Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>Deletes the secret stored under the given key.</summary>
        Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
    }
}
