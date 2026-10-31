using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>pull_request_review_comment</c> event.</summary>
public class PullRequestReviewCommentPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public GitHubReviewComment Comment { get; set; } = new();

    [JsonPropertyName("pull_request")]
    public GitHubPullRequest PullRequest { get; set; } = new();
}

public class GitHubReviewComment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("pull_request_review_id")]
    public long? PullRequestReviewId { get; set; }

    [JsonPropertyName("diff_hunk")]
    public string DiffHunk { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int? Position { get; set; }

    [JsonPropertyName("original_position")]
    public int? OriginalPosition { get; set; }

    [JsonPropertyName("commit_id")]
    public string CommitId { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubUser User { get; set; } = new();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
