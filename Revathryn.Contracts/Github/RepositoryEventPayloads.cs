using System;
using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

/// <summary>Payload for the <c>repository</c> event.</summary>
public class RepositoryPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
}

/// <summary>Payload for the <c>public</c> event (repository made public).</summary>
public class PublicPayload : GitHubWebhookPayload
{
    // No additional fields beyond base; GitHub sends this when a repo is made public.
}

/// <summary>Payload for the <c>milestone</c> event.</summary>
public class MilestonePayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("milestone")]
    public GitHubMilestone Milestone { get; set; } = new();
}

/// <summary>Payload for the <c>label</c> event.</summary>
public class LabelPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public GitHubLabel Label { get; set; } = new();
}

/// <summary>Payload for the <c>project</c> event (classic projects).</summary>
public class ProjectPayload : GitHubWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public GitHubProject Project { get; set; } = new();
}

public class GitHubProject
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("creator")]
    public GitHubUser Creator { get; set; } = new();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
