using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>issues</c> event.</summary>
public class IssuesPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("issue")]
    public GitHubIssue Issue { get; set; } = new();

    [JsonPropertyName("label")]
    public GitHubLabel Label { get; set; }

    [JsonPropertyName("assignee")]
    public GitHubUser Assignee { get; set; }

    [JsonPropertyName("milestone")]
    public GitHubMilestone Milestone { get; set; }
}

public class GitHubIssue
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("user")]
    public GitHubUser User { get; set; } = new();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public List<GitHubLabel> Labels { get; set; } = [];

    [JsonPropertyName("assignees")]
    public List<GitHubUser> Assignees { get; set; } = [];

    [JsonPropertyName("milestone")]
    public GitHubMilestone Milestone { get; set; }

    [JsonPropertyName("comments")]
    public int Comments { get; set; }

    [JsonPropertyName("pull_request")]
    public GitHubIssuePullRequest PullRequest { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTimeOffset? ClosedAt { get; set; }
}

public class GitHubIssuePullRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("merged_at")]
    public DateTimeOffset? MergedAt { get; set; }
}
