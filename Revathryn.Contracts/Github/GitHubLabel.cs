using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

public class GitHubLabel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public bool Default { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}
