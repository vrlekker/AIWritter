using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GhostEngine.Models;

const int FeedItemCount = 6;
const int GitHubFetchCount = 24;
const int HackerNewsFetchCount = 20;

var utcNow = DateTime.UtcNow;
var recentDate = utcNow.AddDays(-7).ToString("yyyy-MM-dd");

var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var dataDirectory = Path.Combine(rootPath, "src", "MainSite", "data");
var outputJsonPath = Path.Combine(dataDirectory, "trending-repos.json");
var sitemapPath = Path.Combine(rootPath, "src", "MainSite", "sitemap.xml");

Directory.CreateDirectory(dataDirectory);

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GhostEngine", "1.0"));
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (!string.IsNullOrWhiteSpace(githubToken))
{
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
}

var csharpQuery = $"language:C# stars:>20 pushed:>{recentDate} archived:false";
var aiToolsQuery = "topic:ai stars:>150 archived:false";

var feeds = new List<PortalFeed>
{
    await BuildGitHubFeedAsync(
        httpClient,
        id: "csharp-repos",
        title: "Trending C# Repositories",
        description: "Fresh .NET and C# projects gaining attention this week.",
        query: csharpQuery,
        sort: "stars",
        order: "desc",
        actionLabel: "View Repository",
        takeCount: FeedItemCount),
    await BuildGitHubFeedAsync(
        httpClient,
        id: "ai-tools",
        title: "AI Tools",
        description: "Active AI tooling repositories worth monitoring across the ecosystem.",
        query: aiToolsQuery,
        sort: "updated",
        order: "desc",
        actionLabel: "Explore Tool",
        takeCount: FeedItemCount),
    await BuildHackerNewsFeedAsync(
        httpClient,
        id: "tech-news",
        title: "Latest Tech News",
        description: "Hot technology headlines from Hacker News top stories.",
        actionLabel: "Read Article",
        takeCount: FeedItemCount)
};

feeds = feeds.Where(feed => feed.Items.Count > 0).ToList();

var payload = new TrendingReposPayload
{
    GeneratedAtUtc = utcNow.ToString("O"),
    FeaturedFeedId = feeds.FirstOrDefault()?.Id ?? string.Empty,
    TotalItemCount = feeds.Sum(feed => feed.Items.Count),
    Feeds = feeds,
    Items = feeds.FirstOrDefault()?.Items ?? new List<PortalCardItem>()
};

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

await File.WriteAllTextAsync(outputJsonPath, JsonSerializer.Serialize(payload, options), Encoding.UTF8);
await WriteSitemapAsync(sitemapPath, feeds, utcNow);

Console.WriteLine($"Wrote {payload.TotalItemCount} records across {feeds.Count} feeds to {outputJsonPath}");
Console.WriteLine($"Updated sitemap at {sitemapPath}");

static async Task<PortalFeed> BuildGitHubFeedAsync(
    HttpClient client,
    string id,
    string title,
    string description,
    string query,
    string sort,
    string order,
    string actionLabel,
    int takeCount)
{
    var feed = new PortalFeed
    {
        Id = id,
        Title = title,
        Description = description,
        ItemType = "repository",
        ActionLabel = actionLabel,
        SourceQuery = query
    };

    try
    {
        var searchUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(query)}&sort={sort}&order={order}&per_page={GitHubFetchCount}";
        var searchJson = await client.GetStringAsync(searchUrl);
        var searchResponse = JsonSerializer.Deserialize<GitHubSearchResponse>(searchJson);

        if (searchResponse is null)
        {
            return feed;
        }

        foreach (var repo in searchResponse.Items.Where(IsAllowedRepository).Take(takeCount))
        {
            var readmeText = await TryGetReadmeAsync(client, repo.FullName);
            var summary = await SummarizeReadmeAsync(client, repo, readmeText);

            feed.Items.Add(new PortalCardItem
            {
                Title = repo.Name,
                Subtitle = $"@{repo.Owner.Login}",
                Summary = summary,
                Url = repo.HtmlUrl,
                Category = repo.Language ?? "Repository",
                Metric = $"{repo.StargazersCount:N0} stars",
                PublishedAtUtc = repo.UpdatedAt == default ? DateTime.UtcNow.ToString("O") : repo.UpdatedAt.ToUniversalTime().ToString("O")
            });
        }
    }
    catch
    {
        return feed;
    }

    return feed;
}

static async Task<PortalFeed> BuildHackerNewsFeedAsync(
    HttpClient client,
    string id,
    string title,
    string description,
    string actionLabel,
    int takeCount)
{
    var feed = new PortalFeed
    {
        Id = id,
        Title = title,
        Description = description,
        ItemType = "news",
        ActionLabel = actionLabel,
        SourceQuery = "https://hacker-news.firebaseio.com/v0/topstories.json"
    };

    try
    {
        var storyIds = await client.GetFromJsonAsync<List<int>>("https://hacker-news.firebaseio.com/v0/topstories.json");
        if (storyIds is null)
        {
            return feed;
        }

        var storyTasks = storyIds.Take(HackerNewsFetchCount)
            .Select(idValue => TryGetHackerNewsItemAsync(client, idValue));

        var stories = await Task.WhenAll(storyTasks);
        foreach (var story in stories
                     .Where(story => story is not null)
                     .Where(story => string.Equals(story!.Type, "story", StringComparison.OrdinalIgnoreCase))
                     .Where(story => !string.IsNullOrWhiteSpace(story!.Title) && !string.IsNullOrWhiteSpace(story.Url))
                     .Take(takeCount))
        {
            feed.Items.Add(new PortalCardItem
            {
                Title = NormalizeSummaryText(story!.Title, 110),
                Subtitle = $"Hacker News • @{story.By}",
                Summary = BuildHackerNewsSummary(story),
                Url = story.Url!,
                Category = "Technology News",
                Metric = $"{story.Score:N0} points",
                PublishedAtUtc = DateTimeOffset.FromUnixTimeSeconds(story.Time).UtcDateTime.ToString("O")
            });
        }
    }
    catch
    {
        return feed;
    }

    return feed;
}

static async Task<HackerNewsItem?> TryGetHackerNewsItemAsync(HttpClient client, int id)
{
    try
    {
        return await client.GetFromJsonAsync<HackerNewsItem>($"https://hacker-news.firebaseio.com/v0/item/{id}.json");
    }
    catch
    {
        return null;
    }
}

static string BuildHackerNewsSummary(HackerNewsItem story)
{
    var comments = story.Descendants > 0 ? $" and {story.Descendants:N0} comments" : string.Empty;
    return NormalizeSummaryText($"Trending on Hacker News with {story.Score:N0} points{comments}. Fresh coverage from the technology discussion cycle.", 170);
}

static bool IsAllowedRepository(GitHubRepositoryItem repo)
{
    var blockedTerms = new[]
    {
        "skin changer",
        "cheat",
        "aimbot",
        "wallhack",
        "exploit",
        "spoofer",
        "crack",
        "hwid",
        "unlock all",
        "mod menu",
        "bypass",
        "free download"
    };

    var combined = $"{repo.FullName} {repo.Description}".ToLowerInvariant();
    return blockedTerms.All(term => !combined.Contains(term, StringComparison.OrdinalIgnoreCase));
}

static async Task<string> TryGetReadmeAsync(HttpClient client, string fullName)
{
    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{fullName}/readme");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync();
    }
    catch
    {
        return string.Empty;
    }
}

static async Task<string> SummarizeReadmeAsync(HttpClient client, GitHubRepositoryItem repo, string readmeText)
{
    if (string.IsNullOrWhiteSpace(readmeText))
    {
        return NormalizeSummaryText(repo.Description, 180);
    }

    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return CreateFallbackSummary(readmeText, repo.Description);
    }

    var maxReadmeChars = 4000;
    var prompt = readmeText.Length > maxReadmeChars ? readmeText[..maxReadmeChars] : readmeText;

    var requestBody = new
    {
        model,
        temperature = 0.2,
        messages = new object[]
        {
            new { role = "system", content = "Summarize repository README text in 2 concise sentences for a professional developer portal." },
            new { role = "user", content = $"Repository: {repo.FullName}\n\nREADME:\n{prompt}" }
        }
    };

    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return CreateFallbackSummary(readmeText, repo.Description);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? CreateFallbackSummary(readmeText, repo.Description)
            : NormalizeSummaryText(content, 180);
    }
    catch
    {
        return CreateFallbackSummary(readmeText, repo.Description);
    }
}

static string CreateFallbackSummary(string readmeText, string? description)
{
    var clean = NormalizeSummaryText(readmeText, 400);
    var normalizedDescription = NormalizeSummaryText(description, 120);

    if (string.IsNullOrWhiteSpace(clean))
    {
        return string.IsNullOrWhiteSpace(normalizedDescription)
            ? "No README summary available."
            : normalizedDescription;
    }

    var excerpt = ExtractSummaryExcerpt(clean, normalizedDescription);

    if (string.IsNullOrWhiteSpace(normalizedDescription))
    {
        return excerpt;
    }

    if (string.IsNullOrWhiteSpace(excerpt) || string.Equals(excerpt, normalizedDescription, StringComparison.OrdinalIgnoreCase))
    {
        return normalizedDescription;
    }

    return NormalizeSummaryText($"{normalizedDescription} {excerpt}", 180);
}

static string NormalizeSummaryText(string? value, int maxLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var clean = value;
    clean = Regex.Replace(clean, @"```.*?```", " ", RegexOptions.Singleline);
    clean = Regex.Replace(clean, @"!\[[^\]]*\]\([^)]+\)", " ");
    clean = Regex.Replace(clean, @"\[([^\]]+)\]\([^)]+\)", "$1");
    clean = Regex.Replace(clean, @"https?://\S+", " ");
    clean = Regex.Replace(clean, @"<[^>]+>", " ");
    clean = Regex.Replace(clean, @"(?m)^\s{0,3}(#{1,6}|>+|[-*+])\s*", " ");
    clean = clean.Replace("|", " ").Replace("`", string.Empty).Replace("*", string.Empty);
    clean = clean.Replace("\r", " ").Replace("\n", " ");
    clean = Regex.Replace(clean, @"\s+", " ").Trim();

    if (clean.Length <= maxLength)
    {
        return clean;
    }

    var shortened = clean[..maxLength].TrimEnd();
    var lastSpace = shortened.LastIndexOf(' ');
    if (lastSpace > maxLength / 2)
    {
        shortened = shortened[..lastSpace];
    }

    return shortened.TrimEnd(' ', '.', ',', ';', ':') + "...";
}

static string ExtractSummaryExcerpt(string cleanText, string normalizedDescription)
{
    if (string.IsNullOrWhiteSpace(cleanText))
    {
        return string.Empty;
    }

    var sentences = Regex.Split(cleanText, @"(?<=[.!?])\s+")
        .Select(segment => segment.Trim())
        .Where(segment => segment.Length >= 32)
        .Where(segment => string.IsNullOrWhiteSpace(normalizedDescription) || !segment.Contains(normalizedDescription, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (sentences.Count == 0)
    {
        return NormalizeSummaryText(cleanText, 140);
    }

    return NormalizeSummaryText(sentences[0], 140);
}

static Task WriteSitemapAsync(string sitemapPath, IEnumerable<PortalFeed> feeds, DateTime updatedAtUtc)
{
    var baseUrl = ResolveBaseSiteUrl();
    var lastMod = updatedAtUtc.ToString("yyyy-MM-dd");
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    var urlset = new XElement(ns + "urlset",
        new XElement(ns + "url",
            new XElement(ns + "loc", baseUrl),
            new XElement(ns + "lastmod", lastMod),
            new XElement(ns + "changefreq", "daily"),
            new XElement(ns + "priority", "1.0")
        ));

    foreach (var feed in feeds)
    {
        var feedLink = $"{baseUrl}?feed={Uri.EscapeDataString(feed.Id)}";
        urlset.Add(new XElement(ns + "url",
            new XElement(ns + "loc", feedLink),
            new XElement(ns + "lastmod", lastMod),
            new XElement(ns + "changefreq", "daily"),
            new XElement(ns + "priority", "0.8")
        ));

        foreach (var item in feed.Items)
        {
            var itemLink = $"{baseUrl}?feed={Uri.EscapeDataString(feed.Id)}&item={Uri.EscapeDataString(Slugify(item.Title))}";
            urlset.Add(new XElement(ns + "url",
                new XElement(ns + "loc", itemLink),
                new XElement(ns + "lastmod", lastMod),
                new XElement(ns + "changefreq", "daily"),
                new XElement(ns + "priority", "0.6")
            ));
        }
    }

    var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);
    Directory.CreateDirectory(Path.GetDirectoryName(sitemapPath)!);
    doc.Save(sitemapPath);

    return Task.CompletedTask;
}

static string Slugify(string value)
{
    var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
}

static string ResolveBaseSiteUrl()
{
    var explicitBase = Environment.GetEnvironmentVariable("SITE_BASE_URL");
    if (!string.IsNullOrWhiteSpace(explicitBase))
    {
        return explicitBase.TrimEnd('/') + "/";
    }

    var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
    if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
    {
        return "https://example.github.io/repository/";
    }

    var parts = repository.Split('/');
    return $"https://{parts[0]}.github.io/{parts[1]}/";
}
