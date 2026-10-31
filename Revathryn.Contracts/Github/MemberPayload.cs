using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>member</c> event (collaborator added/removed).</summary>
public class MemberPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("member")]
    public GitHubUser Member { get; set; } = new();

    [JsonPropertyName("changes")]
    public MemberChanges Changes { get; set; }
}

public class MemberChanges
{
    [JsonPropertyName("permission")]
    public MemberPermissionChange Permission { get; set; }
}

public class MemberPermissionChange
{
    [JsonPropertyName("from")]
    public string From { get; set; }

    [JsonPropertyName("to")]
    public string To { get; set; }
}

/// <summary>Payload for the <c>membership</c> event (team membership changes).</summary>
public class MembershipPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("member")]
    public GitHubUser Member { get; set; } = new();

    [JsonPropertyName("team")]
    public GitHubTeam Team { get; set; } = new();
}
