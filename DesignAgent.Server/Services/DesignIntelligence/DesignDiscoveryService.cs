using DesignAgent.Server.Models.DesignIntelligence;
using DesignAgent.Server.Clients;
using AgentContracts.Services;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Text.Json;
using System.Text;
using System.Xml.Linq;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for discovering new design sources using LLM-driven search with quota tracking and Polly resilience
/// </summary>
public class DesignDiscoveryService : IDesignDiscoveryService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IDesignIntelligenceStorage _storage;
    private readonly ILogger<DesignDiscoveryService> _logger;
    private readonly DesignIntelligenceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SearchQuotaTracker _quotaTracker;
    private readonly AsyncRetryPolicy<List<string>> _retryPolicy;

    public DesignDiscoveryService(
        IOllamaClient ollamaClient,
        IDesignIntelligenceStorage storage,
        ILogger<DesignDiscoveryService> logger,
        IOptions<DesignIntelligenceOptions> options,
        IHttpClientFactory httpClientFactory,
        SearchQuotaTracker quotaTracker)
    {
        _ollamaClient = ollamaClient;
        _storage = storage;
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("SearchClient");
        _quotaTracker = quotaTracker;
        
        // Configure Polly retry policy: 3 retries with exponential backoff
        _retryPolicy = Policy<List<string>>
            .Handle<HttpRequestException>(ex => ex.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Search API call failed, retrying in {Delay}s (attempt {Retry}/3): {Error}",
                        timespan.TotalSeconds, retryCount, outcome.Exception?.Message);
                });
    }

    /// <summary>
    /// Generate search queries using LLM
    /// </summary>
    public async Task<List<string>> GenerateSearchQueriesAsync(int count = 5, string? category = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Generating {Count} search queries for category: {Category}", count, category ?? "any");

        // Get prompt from Lightning (or use fallback)
        var systemPrompt = await _storage.GetPromptAsync("design_source_discovery", cancellationToken) 
            ?? GetFallbackDiscoveryPrompt();

        var userPrompt = BuildDiscoveryUserPrompt(count, category);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        var queries = ParseSearchQueries(response.Response);

        _logger.LogInformation("✅ Generated {Count} search queries", queries.Count);
        return queries;
    }

    /// <summary>
    /// Execute search query and return URLs with quota-aware provider rotation and Polly resilience
    /// </summary>
    public async Task<List<string>> SearchDesignSourcesAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Searching: {Query}", query);
        
        // Reset expired quotas before checking
        _quotaTracker.ResetExpiredQuotas();

        // Define provider rotation order (will try each in sequence if one fails)
        var allProviders = new List<string> { "google", "bing", "brave", "duckduckgo", "serper" };
        
        // Start with the configured provider, then try others
        var primaryProvider = _options.SearchProvider?.ToLower() ?? "google";
        if (!allProviders.Contains(primaryProvider))
        {
            allProviders.Insert(0, primaryProvider);
        }
        else
        {
            // Move primary provider to front
            allProviders.Remove(primaryProvider);
            allProviders.Insert(0, primaryProvider);
        }

        // Filter providers that have quota remaining
        var availableProviders = allProviders
            .Where(p => _quotaTracker.HasQuotaRemaining(p))
            .ToList();
            
        if (!availableProviders.Any())
        {
            _logger.LogWarning("⚠️ No providers with quota remaining, falling back to unlimited HTML scrapers");
            availableProviders = new List<string> { "duckduckgo" };
        }
        else
        {
            _logger.LogInformation("📊 Available providers with quota: {Providers}", string.Join(", ", availableProviders));
            foreach (var provider in availableProviders.Where(p => p != "duckduckgo"))
            {
                var (daily, monthly) = _quotaTracker.GetRemainingCalls(provider);
                _logger.LogDebug("Provider {Provider} remaining: Daily={Daily}, Monthly={Monthly}", 
                    provider, daily?.ToString() ?? "unlimited", monthly?.ToString() ?? "unlimited");
            }
        }

        // Try each provider in sequence until one succeeds
        Exception? lastException = null;
        foreach (var provider in availableProviders)
        {
            try
            {
                _logger.LogDebug("Trying search provider: {Provider}", provider);
                
                // Use Polly retry policy for resilience (3 retries with exponential backoff)
                var results = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return provider switch
                    {
                        "google" => await SearchGoogleAsync(query, limit, cancellationToken),
                        "bing" => await SearchBingAsync(query, limit, cancellationToken),
                        "brave" => await SearchBraveAsync(query, limit, cancellationToken),
                        "duckduckgo" => await SearchDuckDuckGoAsync(query, limit, cancellationToken),
                        "serper" => await SearchSerperAsync(query, limit, cancellationToken),
                        _ => new List<string>()
                    };
                });

                if (results.Any())
                {
                    // Record successful API call for quota tracking
                    _quotaTracker.RecordCall(provider);
                    
                    _logger.LogInformation("✅ Search successful using {Provider}: {Count} results", provider, results.Count);
                    return results;
                }
                
                _logger.LogWarning("Provider {Provider} returned no results, trying next...", provider);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Rate limit hit on {Provider} (429), trying next provider...", provider);
                lastException = ex;
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search failed on {Provider} after retries, trying next provider...", provider);
                lastException = ex;
                continue;
            }
        }

        // All providers failed
        _logger.LogError(lastException, "All search providers failed for query: {Query}", query);
        return new List<string>();
    }

    /// <summary>
    /// Evaluate a search result using LLM
    /// </summary>
    public async Task<DesignSource?> EvaluateSearchResultAsync(string url, string searchQuery, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🧠 Evaluating: {Url}", url);

        // Check if already evaluated
        var existing = await _storage.GetSourceByUrlAsync(url, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("⏭️  Already evaluated: {Url}", url);
            return null;
        }

        // Get prompt from Lightning (or use fallback)
        var systemPrompt = await _storage.GetPromptAsync("design_source_evaluation", cancellationToken)
            ?? GetFallbackEvaluationPrompt();

        var userPrompt = BuildEvaluationUserPrompt(url, searchQuery);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        var evaluation = ParseEvaluation(response.Response);

        if (!evaluation.IsDesignWorthy)
        {
            _logger.LogInformation("❌ Not design-worthy: {Url} - {Reason}", url, evaluation.Reason);
            return null;
        }

        // Create design source
        var source = new DesignSource
        {
            Url = NormalizeUrl(url),
            Category = evaluation.Category ?? "general",
            TrustScore = evaluation.TrustScore,
            DiscoveryMethod = "search",
            DiscoveryQuery = searchQuery,
            Tags = evaluation.Tags,
            Status = "pending"
        };

        _logger.LogInformation("✅ Design-worthy: {Url} (Trust: {Trust}, Category: {Category})", 
            url, evaluation.TrustScore, evaluation.Category);

        return source;
    }

    /// <summary>
    /// Run full discovery cycle
    /// </summary>
    public async Task<int> RunDiscoveryCycleAsync(int targetCount = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Starting discovery cycle (target: {Target} sources)", targetCount);

        var discoveredCount = 0;
        var attemptsCount = 0;
        var maxAttempts = targetCount * 3; // Try 3x as many to account for rejections

        while (discoveredCount < targetCount && attemptsCount < maxAttempts)
        {
            // Generate search queries
            var queries = await GenerateSearchQueriesAsync(_options.SearchQueriesPerRun, cancellationToken: cancellationToken);

            foreach (var query in queries)
            {
                if (discoveredCount >= targetCount || attemptsCount >= maxAttempts)
                    break;

                // Search
                var urls = await SearchDesignSourcesAsync(query, _options.SearchResultsPerQuery, cancellationToken);

                foreach (var url in urls)
                {
                    if (discoveredCount >= targetCount || attemptsCount >= maxAttempts)
                        break;

                    attemptsCount++;

                    // Evaluate
                    var source = await EvaluateSearchResultAsync(url, query, cancellationToken);

                    if (source != null)
                    {
                        // Store
                        await _storage.StoreSourceAsync(source, cancellationToken);
                        discoveredCount++;

                        _logger.LogInformation("📈 Progress: {Discovered}/{Target} sources discovered ({Attempts} attempts)", 
                            discoveredCount, targetCount, attemptsCount);
                    }

                    // Throttle to be respectful
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        _logger.LogInformation("🎯 Discovery cycle complete: {Discovered} sources discovered in {Attempts} attempts", 
            discoveredCount, attemptsCount);

        return discoveredCount;
    }

    /// <summary>
    /// Seed curated sources from initial list
    /// </summary>
    public async Task<int> SeedCuratedSourcesAsync(List<DesignSource> sources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🌱 Seeding {Count} curated sources", sources.Count);

        var seededCount = 0;

        foreach (var source in sources)
        {
            // Check if already exists
            var existing = await _storage.GetSourceByUrlAsync(source.Url, cancellationToken);
            if (existing != null)
            {
                _logger.LogDebug("⏭️  Already exists: {Url}", source.Url);
                continue;
            }

            // Mark as curated and high trust
            source.DiscoveryMethod = "curated";
            source.TrustScore = Math.Max(source.TrustScore, 8.0); // Curated sources get at least 8.0
            source.Status = "pending";

            await _storage.StoreSourceAsync(source, cancellationToken);
            seededCount++;
        }

        _logger.LogInformation("✅ Seeded {Count} curated sources", seededCount);
        return seededCount;
    }

    // ===== SEARCH IMPLEMENTATIONS =====

    private async Task<List<string>> SearchGoogleAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.SearchApiKey))
        {
            throw new InvalidOperationException("Google Search API key not configured");
        }
        
        if (string.IsNullOrEmpty(_options.SearchEngineId))
        {
            throw new InvalidOperationException("Google Search Engine ID not configured");
        }

        // Google Custom Search JSON API
        // https://developers.google.com/custom-search/v1/overview
        var apiUrl = $"https://www.googleapis.com/customsearch/v1?key={_options.SearchApiKey}&cx={_options.SearchEngineId}&q={Uri.EscapeDataString(query)}&num={Math.Min(limit, 10)}";

        var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var urls = new List<string>();
        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("link", out var link))
                {
                    urls.Add(link.GetString() ?? string.Empty);
                }
            }
        }

        return urls;
    }

    private async Task<List<string>> SearchBingAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var apiKey = _options.BingApiKey ?? _options.SearchApiKey;
        
        // Try API first if key is available
        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                return await SearchBingApiAsync(query, limit, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bing API failed, falling back to HTML scraping");
            }
        }
        
        // Fallback to RSS feed (free, unlimited, fast!)
        _logger.LogDebug("Using Bing RSS feed (no API key configured)");
        return await SearchBingHtmlAsync(query, limit, cancellationToken);
    }

    private async Task<List<string>> SearchBingApiAsync(string query, int limit, string apiKey, CancellationToken cancellationToken)
    {
        // Bing Web Search API
        // https://docs.microsoft.com/en-us/bing/search-apis/bing-web-search/overview
        var apiUrl = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={limit}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

        var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var urls = new List<string>();
        if (doc.RootElement.TryGetProperty("webPages", out var webPages) &&
            webPages.TryGetProperty("value", out var values))
        {
            foreach (var value in values.EnumerateArray())
            {
                if (value.TryGetProperty("url", out var url))
                {
                    urls.Add(url.GetString() ?? string.Empty);
                }
            }
        }

        _logger.LogInformation("Bing API returned {Count} results", urls.Count);
        return urls;
    }

    private async Task<List<string>> SearchBingHtmlAsync(string query, int limit, CancellationToken cancellationToken)
    {
        // Use Bing RSS feed - much faster and more reliable than HTML scraping or Selenium!
        var rssUrl = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&count={limit}&format=rss";

        try
        {
            _logger.LogDebug("Fetching Bing RSS feed for: {Query}", query);
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await _httpClient.GetAsync(rssUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Parse RSS XML
            var xml = XDocument.Parse(xmlContent);
            var ns = xml.Root?.GetDefaultNamespace() ?? XNamespace.None;
            
            var urls = new List<string>();
            
            // Extract <link> elements from RSS items
            var items = xml.Descendants("item");
            
            _logger.LogDebug("Found {Count} RSS items", items.Count());
            
            foreach (var item in items)
            {
                var link = item.Element("link")?.Value?.Trim();
                
                if (string.IsNullOrWhiteSpace(link))
                    continue;
                
                // Skip Bing's internal links
                if (link.StartsWith("http") && 
                    !link.Contains("bing.com") && 
                    !link.Contains("microsoft.com") &&
                    !link.Contains("microsofttranslator.com"))
                {
                    // Clean up URL (remove tracking parameters)
                    if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
                    {
                        var cleanUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                        urls.Add(cleanUrl);
                        
                        if (urls.Count >= limit)
                            break;
                    }
                }
            }
            
            _logger.LogInformation("Bing RSS feed returned {Count} results", urls.Count);
            
            if (urls.Count == 0)
            {
                _logger.LogWarning("Bing RSS feed returned 0 results. XML length: {Length} chars", xmlContent.Length);
                _logger.LogDebug("RSS XML sample: {Sample}", 
                    xmlContent.Length > 500 ? xmlContent.Substring(0, 500) : xmlContent);
            }
            
            return urls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bing RSS feed fetch failed");
            return new List<string>();
        }
    }

    private async Task<List<string>> SearchBraveAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var apiKey = _options.BraveApiKey ?? _options.SearchApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Brave Search API key not configured");
        }

        // Brave Search API
        // https://brave.com/search/api/
        var apiUrl = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={limit}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", apiKey);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        try
        {
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var urls = new List<string>();
            if (doc.RootElement.TryGetProperty("web", out var web) &&
                web.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("url", out var url))
                    {
                        urls.Add(url.GetString() ?? string.Empty);
                    }
                }
            }

            _logger.LogInformation("Brave Search returned {Count} results", urls.Count);
            return urls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Brave Search failed");
            return new List<string>();
        }
    }

    private async Task<List<string>> SearchDuckDuckGoAsync(string query, int limit, CancellationToken cancellationToken)
    {
        // DuckDuckGo HTML search (no API key required - free fallback)
        // Note: This is a simple HTML scraper and may break if DDG changes their HTML structure
        var apiUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Simple regex to extract URLs from DuckDuckGo HTML results
            // Look for links in the format: href="//duckduckgo.com/l/?uddg=https://..."
            var urls = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(
                html, 
                @"uddg=([^&""]+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var url = Uri.UnescapeDataString(match.Groups[1].Value);
                    if (Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        urls.Add(url);
                        if (urls.Count >= limit) break;
                    }
                }
            }

            _logger.LogInformation("DuckDuckGo returned {Count} results", urls.Count);
            return urls;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DuckDuckGo search failed");
            return new List<string>();
        }
    }

    private async Task<List<string>> SearchSerperAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var apiKey = _options.SerperApiKey ?? _options.SearchApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Serper API key not configured");
        }

        // Serper.dev Google Search API
        // https://serper.dev/
        var apiUrl = "https://google.serper.dev/search";

        var requestBody = new
        {
            q = query,
            num = limit
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

        var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var urls = new List<string>();
        if (doc.RootElement.TryGetProperty("organic", out var organic))
        {
            foreach (var result in organic.EnumerateArray())
            {
                if (result.TryGetProperty("link", out var link))
                {
                    urls.Add(link.GetString() ?? string.Empty);
                }
            }
        }

        return urls;
    }

    // ===== PROMPT BUILDERS =====

    private string BuildDiscoveryUserPrompt(int count, string? category)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate {count} diverse search queries to discover high-quality design websites.");
        
        if (!string.IsNullOrEmpty(category))
        {
            sb.AppendLine($"Focus on the category: {category}");
        }
        
        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine("- Mix specific company names (e.g., 'Linear app design') and general queries (e.g., 'best SaaS designs 2024')");
        sb.AppendLine("- Include award sites (Awwwards, CSS Design Awards) and curated galleries");
        sb.AppendLine("- Vary between industries: SaaS, e-commerce, portfolios, developer tools");
        sb.AppendLine("- Include terms like 'minimal design', 'modern UI', 'best UX', 'design inspiration'");
        sb.AppendLine();
        sb.AppendLine("Return ONLY the search queries, one per line, no numbering or extra text.");
        
        return sb.ToString();
    }

    private string BuildEvaluationUserPrompt(string url, string searchQuery)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Evaluate if this URL is worth analyzing for design quality:");
        sb.AppendLine($"URL: {url}");
        sb.AppendLine($"Found via search: {searchQuery}");
        sb.AppendLine();
        sb.AppendLine("Criteria for being design-worthy:");
        sb.AppendLine("1. Is it a real website (not a gallery, blog post ABOUT design, or tool)?");
        sb.AppendLine("2. Does it have a custom design (not a template or generic WordPress theme)?");
        sb.AppendLine("3. Is it a product/company site, portfolio, or SaaS app?");
        sb.AppendLine("4. Does it appear to be actively maintained (not defunct)?");
        sb.AppendLine();
        sb.AppendLine("HIGH TRUST (8-10): Awwwards winners, well-known tech companies, Y Combinator startups");
        sb.AppendLine("MEDIUM TRUST (5-7): Lesser-known but professional sites");
        sb.AppendLine("LOW TRUST (1-4): Personal blogs, templates, outdated sites");
        sb.AppendLine();
        sb.AppendLine("Return JSON:");
        sb.AppendLine("{");
        sb.AppendLine("  \"isDesignWorthy\": true/false,");
        sb.AppendLine("  \"trustScore\": 1-10,");
        sb.AppendLine("  \"category\": \"saas|ecommerce|portfolio|developer-tools|other\",");
        sb.AppendLine("  \"tags\": [\"minimal\", \"gradient\", \"dark-mode\", etc],");
        sb.AppendLine("  \"reason\": \"Brief explanation\"");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    // ===== FALLBACK PROMPTS =====

    private string GetFallbackDiscoveryPrompt()
    {
        return @"You are a design discovery expert. Your job is to generate search queries that will find high-quality, design-worthy websites. Focus on:
- Modern SaaS products with excellent UI/UX
- Award-winning websites (Awwwards, CSS Design Awards)
- Well-designed e-commerce sites
- Beautiful portfolios and agencies
- Developer tools with great design

Generate diverse queries that mix specific brands, general design searches, and curated galleries.";
    }

    private string GetFallbackEvaluationPrompt()
    {
        return @"You are a design evaluation expert. Your job is to quickly assess if a URL is worth crawling and analyzing for design quality. 

Be selective - we only want the best designs. Reject:
- Blog posts about design
- Design galleries/collections (we want the actual sites, not galleries)
- Generic templates
- Outdated or broken sites
- Sites with poor design

Accept:
- Actual product/company websites with custom design
- SaaS applications
- High-quality portfolios
- Award-winning sites

Return concise JSON with your evaluation.";
    }

    // ===== PARSING =====

    private List<string> ParseSearchQueries(string response)
    {
        // Parse LLM response (expects one query per line)
        var queries = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim())
            .Where(q => !string.IsNullOrWhiteSpace(q) && !q.StartsWith("#") && q.Length > 5)
            .Take(20) // Safety limit
            .ToList();

        return queries;
    }

    private SourceEvaluation ParseEvaluation(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new SourceEvaluation
                {
                    IsDesignWorthy = root.TryGetProperty("isDesignWorthy", out var worthy) && worthy.GetBoolean(),
                    TrustScore = root.TryGetProperty("trustScore", out var trust) ? trust.GetDouble() : 5.0,
                    Category = root.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    Tags = root.TryGetProperty("tags", out var tags) 
                        ? tags.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList()
                        : new List<string>(),
                    Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : ""
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse evaluation JSON, using fallback");
        }

        // Fallback: reject if we can't parse
        return new SourceEvaluation
        {
            IsDesignWorthy = false,
            TrustScore = 5.0,
            Reason = "Failed to parse LLM response"
        };
    }

    private string NormalizeUrl(string url)
    {
        // Remove trailing slash, ensure https
        url = url.Trim().TrimEnd('/');
        
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }

        return url;
    }

    private class SourceEvaluation
    {
        public bool IsDesignWorthy { get; set; }
        public double TrustScore { get; set; }
        public string? Category { get; set; }
        public List<string> Tags { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
    }
}

