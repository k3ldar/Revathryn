using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

public class GitHubCommit
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tree_id")]
    public string TreeId { get; set; } = string.Empty;

    [JsonPropertyName("distinct")]
    public bool Distinct { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public GitHubCommitAuthor Author { get; set; } = new();

    [JsonPropertyName("committer")]
    public GitHubCommitAuthor Committer { get; set; } = new();

    [JsonPropertyName("added")]
    public List<string> Added { get; set; } = [];

    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = [];

    [JsonPropertyName("modified")]
    public List<string> Modified { get; set; } = [];
}

public class GitHubCommitAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; }
}
