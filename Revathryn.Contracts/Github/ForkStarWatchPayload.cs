using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>fork</c> event.</summary>
public class ForkPayload : GitHubWebhookPayload
{
    [JsonPropertyName("forkee")]
    public GitHubRepository Forkee { get; set; } = new();
}

/// <summary>Payload for the <c>star</c> event (previously <c>watch</c>).</summary>
public class StarPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("starred_at")]
    public DateTimeOffset? StarredAt { get; set; }
}

/// <summary>Payload for the <c>watch</c> event (someone stars the repository).</summary>
public class WatchPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
}
