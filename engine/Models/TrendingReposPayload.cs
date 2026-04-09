namespace GhostEngine.Models;

public sealed class TrendingReposPayload
{
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public string SourceQuery { get; set; } = string.Empty;
    public List<TrendingRepoItem> Items { get; set; } = new();
}

public sealed class TrendingRepoItem
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Language { get; set; } = string.Empty;
    public string ReadmeSummary { get; set; } = string.Empty;
    public string LastUpdatedUtc { get; set; } = string.Empty;
}
