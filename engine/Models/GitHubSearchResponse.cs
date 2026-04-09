using System.Text.Json.Serialization;

namespace GhostEngine.Models;

public sealed class GitHubSearchResponse
{
    [JsonPropertyName("items")]
    public List<GitHubRepositoryItem> Items { get; set; } = new();
}

public sealed class GitHubRepositoryItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("owner")]
    public GitHubOwner Owner { get; set; } = new();
}

public sealed class GitHubOwner
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;
}
