using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GhostEngine.Models;

const int RepoCount = 5;
var utcNow = DateTime.UtcNow;
var recentDate = utcNow.AddDays(-7).ToString("yyyy-MM-dd");
var sourceQuery = $"language:C# created:>{recentDate}";

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

var searchUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(sourceQuery)}&sort=stars&order=desc&per_page={RepoCount}";
var searchJson = await httpClient.GetStringAsync(searchUrl);
var searchResponse = JsonSerializer.Deserialize<GitHubSearchResponse>(searchJson);

if (searchResponse is null || searchResponse.Items.Count == 0)
{
    Console.WriteLine("No repositories were returned from GitHub Search API.");
    return;
}

var payload = new TrendingReposPayload
{
    GeneratedAtUtc = utcNow.ToString("O"),
    SourceQuery = sourceQuery
};

foreach (var repo in searchResponse.Items.Take(RepoCount))
{
    var readmeText = await TryGetReadmeAsync(httpClient, repo.FullName);
    var summary = await SummarizeReadmeAsync(httpClient, repo, readmeText);

    payload.Items.Add(new TrendingRepoItem
    {
        Name = repo.Name,
        Owner = repo.Owner.Login,
        Description = repo.Description ?? "No description provided.",
        HtmlUrl = repo.HtmlUrl,
        Stars = repo.StargazersCount,
        Language = repo.Language ?? "Unknown",
        ReadmeSummary = summary,
        LastUpdatedUtc = utcNow.ToString("O")
    });
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

await File.WriteAllTextAsync(outputJsonPath, JsonSerializer.Serialize(payload, options), Encoding.UTF8);
await WriteSitemapAsync(sitemapPath, payload.Items, utcNow);

Console.WriteLine($"Wrote {payload.Items.Count} records to {outputJsonPath}");
Console.WriteLine($"Updated sitemap at {sitemapPath}");

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
        return repo.Description ?? "No README summary available.";
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
            new { role = "system", content = "Summarize repository README text in 2 concise sentences for a developer portal." },
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
            : NormalizeSummaryText(content, 220);
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

    return NormalizeSummaryText($"{normalizedDescription} {excerpt}", 220);
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

static Task WriteSitemapAsync(string sitemapPath, IEnumerable<TrendingRepoItem> repos, DateTime updatedAtUtc)
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

    foreach (var repo in repos)
    {
        var repoParam = Uri.EscapeDataString($"{repo.Owner}/{repo.Name}");
        var link = $"{baseUrl}?repo={repoParam}";
        urlset.Add(new XElement(ns + "url",
            new XElement(ns + "loc", link),
            new XElement(ns + "lastmod", lastMod),
            new XElement(ns + "changefreq", "daily"),
            new XElement(ns + "priority", "0.7")
        ));
    }

    var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);
    Directory.CreateDirectory(Path.GetDirectoryName(sitemapPath)!);
    doc.Save(sitemapPath);

    return Task.CompletedTask;
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
