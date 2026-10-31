using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>status</c> event (commit status change).</summary>
public class StatusPayload : GitHubWebhookPayload
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; }

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public GitHubStatusCommit Commit { get; set; } = new();

    [JsonPropertyName("branches")]
    public List<GitHubBranchRef> Branches { get; set; } = [];

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public class GitHubStatusCommit
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public GitHubStatusCommitDetail Commit { get; set; } = new();

    [JsonPropertyName("author")]
    public GitHubUser Author { get; set; }

    [JsonPropertyName("committer")]
    public GitHubUser Committer { get; set; }
}

public class GitHubStatusCommitDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class GitHubBranchRef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public GitHubBranchCommitRef Commit { get; set; } = new();
}

public class GitHubBranchCommitRef
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
