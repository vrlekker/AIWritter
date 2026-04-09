namespace GhostEngine.Models;

public sealed class TrendingReposPayload
{
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public string FeaturedFeedId { get; set; } = string.Empty;
    public int TotalItemCount { get; set; }
    public List<PortalFeed> Feeds { get; set; } = new();
    public List<PortalCardItem> Items { get; set; } = new();
}

public sealed class PortalFeed
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string SourceQuery { get; set; } = string.Empty;
    public List<PortalCardItem> Items { get; set; } = new();
}

public sealed class PortalCardItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string PublishedAtUtc { get; set; } = string.Empty;
}
