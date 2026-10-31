using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>push</c> event.</summary>
public class PushPayload : GitHubWebhookPayload
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("before")]
    public string Before { get; set; } = string.Empty;

    [JsonPropertyName("after")]
    public string After { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public bool Created { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    [JsonPropertyName("forced")]
    public bool Forced { get; set; }

    [JsonPropertyName("base_ref")]
    public string BaseRef { get; set; }

    [JsonPropertyName("compare")]
    public string Compare { get; set; } = string.Empty;

    [JsonPropertyName("commits")]
    public List<GitHubCommit> Commits { get; set; } = [];

    [JsonPropertyName("head_commit")]
    public GitHubCommit HeadCommit { get; set; }

    [JsonPropertyName("pusher")]
    public GitHubCommitAuthor Pusher { get; set; } = new();
}
