using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Base properties common to all GitHub webhook payloads.</summary>
public abstract class GitHubWebhookPayload
{
    [JsonPropertyName("sender")]
    public GitHubUser Sender { get; set; } = new();

    [JsonPropertyName("repository")]
    public GitHubRepository Repository { get; set; }

    [JsonPropertyName("organization")]
    public GitHubOrganization Organization { get; set; }

    [JsonPropertyName("installation")]
    public GitHubInstallation Installation { get; set; }
}
