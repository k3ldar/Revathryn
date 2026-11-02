using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Engine.Tests
{
    /// <summary>In-memory secrets provider for tests.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class InMemorySecretsProvider : ISecretsProvider
    {
        public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

        public Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Secrets.TryGetValue(key ?? string.Empty, out string value) ? value : null);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Secrets[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            _ = Secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}
