using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>deployment</c> event.</summary>
public class DeploymentPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("deployment")]
    public GitHubDeployment Deployment { get; set; } = new();
}

public class GitHubDeployment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("creator")]
    public GitHubUser Creator { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Payload for the <c>deployment_status</c> event.</summary>
public class DeploymentStatusPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("deployment_status")]
    public GitHubDeploymentStatus DeploymentStatus { get; set; } = new();

    [JsonPropertyName("deployment")]
    public GitHubDeployment Deployment { get; set; } = new();
}

public class GitHubDeploymentStatus
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("creator")]
    public GitHubUser Creator { get; set; } = new();

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; }

    [JsonPropertyName("log_url")]
    public string LogUrl { get; set; }

    [JsonPropertyName("environment_url")]
    public string EnvironmentUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
