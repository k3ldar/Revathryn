using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>ping</c> event — sent when a webhook is first configured.</summary>
public class PingPayload : GitHubWebhookPayload
{
    [JsonPropertyName("zen")]
    public string Zen { get; set; } = string.Empty;

    [JsonPropertyName("hook_id")]
    public long HookId { get; set; }

    [JsonPropertyName("hook")]
    public GitHubHook Hook { get; set; }
}

public class GitHubHook
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    [JsonPropertyName("config")]
    public GitHubHookConfig Config { get; set; } = new();
}

public class GitHubHookConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("insecure_ssl")]
    public string InsecureSsl { get; set; } = string.Empty;
}
