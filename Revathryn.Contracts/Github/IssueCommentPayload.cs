using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>issue_comment</c> event.</summary>
public class IssueCommentPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("issue")]
    public GitHubIssue Issue { get; set; } = new();

    [JsonPropertyName("comment")]
    public GitHubComment Comment { get; set; } = new();
}

public class GitHubComment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubUser User { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
