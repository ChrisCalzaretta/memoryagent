using System.Text.Json;
using System.Web;

namespace CodingAgent.Server.Services;

/// <summary>
/// 🌐 WEB SEARCH SERVICE - Augments LLMs with real-time web knowledge
/// Uses Cursor's web_search tool + provider-specific documentation searches
/// </summary>
public class WebSearchService : IWebSearchService
{
    private readonly ILogger<WebSearchService> _logger;
    private readonly HttpClient _httpClient;
    
    // Cache to avoid redundant searches
    private readonly Dictionary<string, (List<WebSearchResult> Results, DateTime Cached)> _cache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);
    
    // Official documentation URLs by language
    private static readonly Dictionary<string, OfficialDocProvider> _officialDocs = new()
    {
        ["csharp"] = new("Microsoft Docs", "https://learn.microsoft.com/en-us/search/?terms={query}+C%23"),
        ["c#"] = new("Microsoft Docs", "https://learn.microsoft.com/en-us/search/?terms={query}+C%23"),
        ["dotnet"] = new("Microsoft Docs", "https://learn.microsoft.com/en-us/search/?terms={query}+.NET"),
        ["python"] = new("Python.org", "https://docs.python.org/3/search.html?q={query}"),
        ["javascript"] = new("MDN Web Docs", "https://developer.mozilla.org/en-US/search?q={query}"),
        ["typescript"] = new("TypeScript Docs", "https://www.typescriptlang.org/search?q={query}"),
        ["java"] = new("Oracle Java Docs", "https://docs.oracle.com/en/java/javase/21/docs/api/search.html?q={query}"),
        ["go"] = new("Go.dev", "https://pkg.go.dev/search?q={query}"),
        ["rust"] = new("Rust Docs", "https://doc.rust-lang.org/std/?search={query}"),
        ["php"] = new("PHP.net", "https://www.php.net/manual-lookup.php?pattern={query}"),
        ["ruby"] = new("Ruby Docs", "https://ruby-doc.org/search.html?q={query}"),
        ["swift"] = new("Swift.org", "https://www.swift.org/search/?q={query}"),
        ["kotlin"] = new("Kotlin Docs", "https://kotlinlang.org/search.html?q={query}"),
        ["flutter"] = new("Flutter Docs", "https://api.flutter.dev/index.html?q={query}"),
        ["dart"] = new("Dart Docs", "https://api.dart.dev/stable/search.html?q={query}"),
        ["blazor"] = new("Blazor Docs", "https://learn.microsoft.com/en-us/search/?terms={query}+Blazor"),
        ["react"] = new("React Docs", "https://react.dev/?search={query}"),
        ["vue"] = new("Vue Docs", "https://vuejs.org/search/?q={query}"),
        ["angular"] = new("Angular Docs", "https://angular.io/search?q={query}")
    };
    
    public WebSearchService(ILogger<WebSearchService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }
    
    /// <summary>
    /// Research a task comprehensively:
    /// 1. Official docs (provider-specific)
    /// 2. Stack Overflow (community solutions)
    /// 3. GitHub (real-world examples)
    /// 4. Dev blogs (best practices)
    /// </summary>
    public async Task<List<WebSearchResult>> ResearchTaskAsync(
        string task,
        string language,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{language}:{task}";
        
        // Check cache first
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.Cached < _cacheExpiry)
            {
                _logger.LogInformation("📚 Using cached research for {Language}: {Task}", language, task);
                return cached.Results;
            }
        }
        
        _logger.LogInformation("🔍 Researching: {Language} - {Task}", language, task);
        
        var results = new List<WebSearchResult>();
        
        // 1. Official documentation (most authoritative)
        try
        {
            var officialResults = await SearchOfficialDocsAsync(language, task, 3, cancellationToken);
            results.AddRange(officialResults);
            _logger.LogInformation("  ✅ Found {Count} official docs", officialResults.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "  ⚠️ Official docs search failed (non-fatal)");
        }
        
        // 2. General web search (Stack Overflow, GitHub, blogs)
        try
        {
            var webQuery = $"{language} {task} best practices example code";
            var webResults = await SearchWebAsync(webQuery, maxResults - results.Count, cancellationToken);
            results.AddRange(webResults);
            _logger.LogInformation("  ✅ Found {Count} web results", webResults.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "  ⚠️ Web search failed (non-fatal)");
        }
        
        // Cache results
        _cache[cacheKey] = (results, DateTime.UtcNow);
        
        _logger.LogInformation("✅ Research complete: {Total} results", results.Count);
        return results;
    }
    
    /// <summary>
    /// Search official documentation (e.g., Microsoft Docs for C#)
    /// </summary>
    public async Task<List<WebSearchResult>> SearchOfficialDocsAsync(
        string language,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var langKey = language.ToLowerInvariant().Replace(" ", "");
        
        if (!_officialDocs.TryGetValue(langKey, out var provider))
        {
            _logger.LogDebug("No official docs provider for language: {Language}", language);
            return new List<WebSearchResult>();
        }
        
        _logger.LogInformation("📖 Searching {Provider} for: {Query}", provider.Name, query);
        
        // Build search query with site restriction
        var siteQuery = $"{query} site:{GetDomain(provider.SearchUrl)}";
        
        var results = await SearchWebAsync(siteQuery, maxResults, cancellationToken);
        
        // Tag as official docs
        foreach (var result in results)
        {
            if (!result.Tags.Contains("official-docs"))
            {
                result.Tags.Add("official-docs");
            }
            result.Tags.Add(langKey);
        }
        
        return results;
    }
    
    /// <summary>
    /// General web search (uses Cursor's web_search capabilities or fallback)
    /// </summary>
    public async Task<List<WebSearchResult>> SearchWebAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🌐 Web search: {Query}", query);
        
        // NOTE: In a real implementation, this would use a search API like:
        // - Brave Search API (free tier available)
        // - Google Custom Search API
        // - Bing Search API
        // - DuckDuckGo API
        // For now, we'll simulate with curated results based on common patterns
        
        var results = await SimulateSearchAsync(query, maxResults, cancellationToken);
        
        _logger.LogInformation("  ✅ Found {Count} results", results.Count);
        return results;
    }
    
    /// <summary>
    /// Simulate search results (placeholder for real API integration)
    /// In production, replace with actual API calls
    /// </summary>
    private async Task<List<WebSearchResult>> SimulateSearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        // TODO: Replace with real search API integration
        // For now, return structured placeholders that indicate where real results would come from
        
        var results = new List<WebSearchResult>();
        
        var lowerQuery = query.ToLowerInvariant();
        
        // Detect language/framework from query
        var detectedLang = DetectLanguage(lowerQuery);
        
        // Add Stack Overflow result
        results.Add(new WebSearchResult
        {
            Source = "Stack Overflow",
            Title = $"[SEARCH NEEDED] {query}",
            Url = $"https://stackoverflow.com/search?q={HttpUtility.UrlEncode(query)}",
            Snippet = $"⚠️ PLACEHOLDER: In production, search Stack Overflow for: '{query}'\nExpected: Code examples, common pitfalls, community solutions.",
            PublishedDate = DateTime.UtcNow.AddMonths(-6),
            Relevance = 90,
            Tags = new List<string> { detectedLang, "community", "stackoverflow" }
        });
        
        // Add GitHub result
        results.Add(new WebSearchResult
        {
            Source = "GitHub",
            Title = $"[SEARCH NEEDED] {query}",
            Url = $"https://github.com/search?q={HttpUtility.UrlEncode(query)}",
            Snippet = $"⚠️ PLACEHOLDER: In production, search GitHub for: '{query}'\nExpected: Real-world implementations, production code examples.",
            PublishedDate = DateTime.UtcNow.AddMonths(-3),
            Relevance = 85,
            Tags = new List<string> { detectedLang, "github", "real-world" }
        });
        
        // Add official docs result (if applicable)
        if (_officialDocs.TryGetValue(detectedLang, out var provider))
        {
            results.Add(new WebSearchResult
            {
                Source = provider.Name,
                Title = $"[SEARCH NEEDED] {query}",
                Url = provider.SearchUrl.Replace("{query}", HttpUtility.UrlEncode(query)),
                Snippet = $"⚠️ PLACEHOLDER: In production, search {provider.Name} for: '{query}'\nExpected: Official API docs, best practices, authoritative examples.",
                PublishedDate = DateTime.UtcNow.AddMonths(-1),
                Relevance = 95,
                Tags = new List<string> { detectedLang, "official-docs", "authoritative" }
            });
        }
        
        return results.Take(maxResults).ToList();
    }
    
    private string DetectLanguage(string query)
    {
        if (query.Contains("c#") || query.Contains("csharp") || query.Contains("dotnet") || query.Contains(".net"))
            return "csharp";
        if (query.Contains("python") || query.Contains(".py"))
            return "python";
        if (query.Contains("javascript") || query.Contains("js") || query.Contains("node"))
            return "javascript";
        if (query.Contains("typescript") || query.Contains("ts"))
            return "typescript";
        if (query.Contains("java") && !query.Contains("javascript"))
            return "java";
        if (query.Contains("blazor"))
            return "blazor";
        if (query.Contains("react"))
            return "react";
        if (query.Contains("vue"))
            return "vue";
        if (query.Contains("angular"))
            return "angular";
        if (query.Contains("flutter"))
            return "flutter";
        if (query.Contains("dart"))
            return "dart";
            
        return "general";
    }
    
    private string GetDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return "unknown";
        }
    }
}

/// <summary>
/// Official documentation provider info
/// </summary>
internal record OfficialDocProvider(string Name, string SearchUrl);

