using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>pull_request_review</c> event.</summary>
public class PullRequestReviewPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("review")]
    public GitHubReview Review { get; set; } = new();

    [JsonPropertyName("pull_request")]
    public GitHubPullRequest PullRequest { get; set; } = new();
}

public class GitHubReview
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubUser User { get; set; } = new();

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("submitted_at")]
    public DateTimeOffset? SubmittedAt { get; set; }

    [JsonPropertyName("commit_id")]
    public string CommitId { get; set; } = string.Empty;
}
