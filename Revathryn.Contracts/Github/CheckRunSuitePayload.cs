using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>check_run</c> event.</summary>
public class CheckRunPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("check_run")]
    public GitHubCheckRun CheckRun { get; set; } = new();
}

public class GitHubCheckRun
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("conclusion")]
    public string Conclusion { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("check_suite")]
    public GitHubCheckSuiteRef CheckSuite { get; set; }
}

public class GitHubCheckSuiteRef
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("head_branch")]
    public string HeadBranch { get; set; }

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; } = string.Empty;
}

/// <summary>Payload for the <c>check_suite</c> event.</summary>
public class CheckSuitePayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("check_suite")]
    public GitHubCheckSuite CheckSuite { get; set; } = new();
}

public class GitHubCheckSuite
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("head_branch")]
    public string HeadBranch { get; set; }

    [JsonPropertyName("head_sha")]
    public string HeadSha { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("conclusion")]
    public string Conclusion { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
