using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>create</c> event (branch or tag created).</summary>
public class CreatePayload : GitHubWebhookPayload
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("ref_type")]
    public string RefType { get; set; } = string.Empty;

    [JsonPropertyName("master_branch")]
    public string MasterBranch { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("pusher_type")]
    public string PusherType { get; set; } = string.Empty;
}

/// <summary>Payload for the <c>delete</c> event (branch or tag deleted).</summary>
public class DeletePayload : GitHubWebhookPayload
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("ref_type")]
    public string RefType { get; set; } = string.Empty;

    [JsonPropertyName("pusher_type")]
    public string PusherType { get; set; } = string.Empty;
}
