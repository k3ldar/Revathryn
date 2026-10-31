using System.Text.Json.Serialization;

namespace Revathryn.Contracts.Github;

public class GitHubInstallation
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;
}
