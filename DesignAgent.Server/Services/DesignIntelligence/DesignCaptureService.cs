using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Text;
using System.Text.Json;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for capturing design screenshots and DOM using Selenium WebDriver
/// </summary>
public class DesignCaptureService : IDesignCaptureService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IDesignIntelligenceStorage _storage;
    private readonly ILogger<DesignCaptureService> _logger;
    private readonly DesignIntelligenceOptions _options;

    public DesignCaptureService(
        IOllamaClient ollamaClient,
        IDesignIntelligenceStorage storage,
        ILogger<DesignCaptureService> logger,
        IOptions<DesignIntelligenceOptions> options)
    {
        _ollamaClient = ollamaClient;
        _storage = storage;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Crawl a website and capture all important pages
    /// </summary>
    public async Task<CapturedDesign> CrawlWebsiteAsync(DesignSource source, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🕷️ Crawling website: {Url}", source.Url);

        var design = new CapturedDesign
        {
            Url = source.Url,
            CapturedAt = DateTime.UtcNow
        };

        using var driver = CreateWebDriver();

        try
        {
            // 1. Capture homepage
            _logger.LogInformation("📸 Capturing homepage: {Url}", source.Url);
            var homepagePage = await CapturePageInternalAsync(source.Url, "homepage", cancellationToken, driver);
            design.Pages.Add(homepagePage);

            // 2. Extract all links from homepage
            driver.Navigate().GoToUrl(source.Url);
            await Task.Delay(2000, cancellationToken); // Wait for page load
            var allLinks = ExtractLinks(driver, source.Url);
            
            _logger.LogInformation("🔗 Found {Count} links on homepage", allLinks.Count);

            // 3. Use LLM to select important links
            var selectedLinks = await SelectImportantLinksAsync(source.Url, allLinks, _options.MaxPagesPerSite - 1, cancellationToken);
            
            _logger.LogInformation("✅ Selected {Count} important pages to crawl", selectedLinks.Count);

            // 4. Capture selected pages
            foreach (var (url, pageType) in selectedLinks)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    _logger.LogInformation("📸 Capturing {PageType} page: {Url}", pageType, url);
                    var page = await CapturePageInternalAsync(url, pageType, cancellationToken, driver);
                    design.Pages.Add(page);

                    // Throttle between pages
                    await Task.Delay(_options.CrawlDelayMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to capture page: {Url}", url);
                }
            }

            _logger.LogInformation("✅ Crawled {Count} pages for {Url}", design.Pages.Count, source.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl website: {Url}", source.Url);
            throw;
        }

        return design;
    }

    /// <summary>
    /// Capture a single page (screenshots + DOM) - Interface implementation
    /// </summary>
    public Task<PageAnalysis> CapturePageAsync(string url, string pageType, CancellationToken cancellationToken = default)
    {
        return CapturePageInternalAsync(url, pageType, cancellationToken, null);
    }

    /// <summary>
    /// Capture a single page (screenshots + DOM) - Internal with driver reuse
    /// </summary>
    private async Task<PageAnalysis> CapturePageInternalAsync(string url, string pageType, CancellationToken cancellationToken = default, IWebDriver? existingDriver = null)
    {
        var ownDriver = existingDriver == null;
        var driver = existingDriver ?? CreateWebDriver();

        try
        {
            var page = new PageAnalysis
            {
                DesignId = string.Empty, // Will be set later
                Url = url,
                PageType = pageType,
                AnalyzedAt = DateTime.UtcNow
            };

            // Navigate to page
            driver.Navigate().GoToUrl(url);
            
            // Wait for page to load
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
            
            // Additional wait for JavaScript rendering
            await Task.Delay(2000, cancellationToken);

            // Take screenshots at multiple breakpoints
            page.Screenshots = await TakeScreenshotsInternalAsync(url, _options.ScreenshotBreakpoints, cancellationToken, driver);

            // Extract HTML
            page.ExtractedHtml = await ExtractDomInternalAsync(url, cancellationToken, driver);

            // Extract CSS
            page.ExtractedCss = await ExtractCssInternalAsync(url, cancellationToken, driver);

            _logger.LogInformation("✅ Captured page: {Url} ({Type})", url, pageType);

            return page;
        }
        finally
        {
            if (ownDriver)
            {
                driver.Quit();
                driver.Dispose();
            }
        }
    }

    /// <summary>
    /// Use LLM to evaluate which links are most important to crawl
    /// </summary>
    public async Task<Dictionary<string, string>> SelectImportantLinksAsync(string baseUrl, List<string> links, int maxPages = 6, CancellationToken cancellationToken = default)
    {
        if (links.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        // Get prompt from Lightning (or use fallback)
        var systemPrompt = await _storage.GetPromptAsync("design_link_evaluation", cancellationToken)
            ?? GetFallbackLinkEvaluationPrompt();

        var userPrompt = BuildLinkEvaluationPrompt(baseUrl, links, maxPages);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        return ParseLinkSelection(response.Response, links);
    }

    /// <summary>
    /// Take screenshots at multiple breakpoints - Interface implementation
    /// </summary>
    public Task<ScreenshotSet> TakeScreenshotsAsync(string url, int[] breakpoints, CancellationToken cancellationToken = default)
    {
        return TakeScreenshotsInternalAsync(url, breakpoints, cancellationToken, null);
    }

    /// <summary>
    /// Take screenshots at multiple breakpoints - Internal with driver reuse
    /// </summary>
    private async Task<ScreenshotSet> TakeScreenshotsInternalAsync(string url, int[] breakpoints, CancellationToken cancellationToken = default, IWebDriver? existingDriver = null)
    {
        var ownDriver = existingDriver == null;
        var driver = existingDriver ?? CreateWebDriver();

        try
        {
            var screenshots = new ScreenshotSet();
            
            // Ensure screenshot directory exists
            Directory.CreateDirectory(_options.ScreenshotPath);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var domain = new Uri(url).Host.Replace(".", "_");

            foreach (var width in breakpoints)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Resize browser window
                driver.Manage().Window.Size = new System.Drawing.Size(width, 1080);
                await Task.Delay(500, cancellationToken); // Wait for resize

                // Take screenshot
                var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                var filename = $"{domain}_{timestamp}_{width}px.png";
                var filepath = Path.Combine(_options.ScreenshotPath, filename);
                
                screenshot.SaveAsFile(filepath);

                // Map to ScreenshotSet
                if (width >= 1920)
                    screenshots.Desktop = filepath;
                else if (width >= 768)
                    screenshots.Tablet = filepath;
                else
                    screenshots.Mobile = filepath;

                _logger.LogDebug("📸 Screenshot saved: {Filename} ({Width}px)", filename, width);
            }

            return screenshots;
        }
        finally
        {
            if (ownDriver)
            {
                driver.Quit();
                driver.Dispose();
            }
        }
    }

    /// <summary>
    /// Extract DOM/HTML from page - Interface implementation
    /// </summary>
    public Task<string> ExtractDomAsync(string url, CancellationToken cancellationToken = default)
    {
        return ExtractDomInternalAsync(url, cancellationToken, null);
    }

    /// <summary>
    /// Extract DOM/HTML from page - Internal with driver reuse
    /// </summary>
    private async Task<string> ExtractDomInternalAsync(string url, CancellationToken cancellationToken = default, IWebDriver? existingDriver = null)
    {
        var ownDriver = existingDriver == null;
        var driver = existingDriver ?? CreateWebDriver();

        try
        {
            if (ownDriver)
            {
                driver.Navigate().GoToUrl(url);
                await Task.Delay(2000, cancellationToken);
            }

            // Get page source (full HTML)
            var html = driver.PageSource;

            // Simplify HTML (remove scripts, comments, etc. for cleaner analysis)
            html = SimplifyHtml(html);

            return html;
        }
        finally
        {
            if (ownDriver)
            {
                driver.Quit();
                driver.Dispose();
            }
        }
    }

    /// <summary>
    /// Extract CSS from page - Interface implementation
    /// </summary>
    public Task<string> ExtractCssAsync(string url, CancellationToken cancellationToken = default)
    {
        return ExtractCssInternalAsync(url, cancellationToken, null);
    }

    /// <summary>
    /// Extract CSS from page - Internal with driver reuse
    /// </summary>
    private async Task<string> ExtractCssInternalAsync(string url, CancellationToken cancellationToken = default, IWebDriver? existingDriver = null)
    {
        var ownDriver = existingDriver == null;
        var driver = existingDriver ?? CreateWebDriver();

        try
        {
            if (ownDriver)
            {
                driver.Navigate().GoToUrl(url);
                await Task.Delay(2000, cancellationToken);
            }

            // Execute JavaScript to extract CSS
            var js = (IJavaScriptExecutor)driver;
            
            var cssScript = @"
                let allCss = '';
                
                // Extract inline styles and stylesheets
                const styleSheets = Array.from(document.styleSheets);
                
                for (const sheet of styleSheets) {
                    try {
                        if (sheet.cssRules) {
                            for (const rule of sheet.cssRules) {
                                allCss += rule.cssText + '\n';
                            }
                        }
                    } catch (e) {
                        // Cross-origin stylesheet, skip
                    }
                }
                
                // Extract CSS variables from :root
                const root = document.documentElement;
                const rootStyles = getComputedStyle(root);
                allCss += '\n/* CSS Variables */\n:root {\n';
                for (let i = 0; i < rootStyles.length; i++) {
                    const prop = rootStyles[i];
                    if (prop.startsWith('--')) {
                        allCss += `  ${prop}: ${rootStyles.getPropertyValue(prop)};\n`;
                    }
                }
                allCss += '}\n';
                
                return allCss;
            ";

            var css = js.ExecuteScript(cssScript)?.ToString() ?? string.Empty;

            return css;
        }
        finally
        {
            if (ownDriver)
            {
                driver.Quit();
                driver.Dispose();
            }
        }
    }

    /// <summary>
    /// Capture video for animation analysis (optional)
    /// </summary>
    public Task<string?> CaptureVideoAsync(string url, int durationSec = 10, CancellationToken cancellationToken = default)
    {
        // Video capture requires additional tools (e.g., FFmpeg)
        // Not implemented in Phase 3, will be added later if needed
        _logger.LogWarning("Video capture not yet implemented");
        return Task.FromResult<string?>(null);
    }

    // ===== PRIVATE HELPERS =====

    private IWebDriver CreateWebDriver()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        // Set page load timeout
        var driver = new ChromeDriver(options);
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

        return driver;
    }

    private List<string> ExtractLinks(IWebDriver driver, string baseUrl)
    {
        var links = new List<string>();
        var baseUri = new Uri(baseUrl);

        try
        {
            var anchorElements = driver.FindElements(By.TagName("a"));

            foreach (var anchor in anchorElements)
            {
                try
                {
                    var href = anchor.GetDomAttribute("href");
                    
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    // Parse and normalize URL
                    if (Uri.TryCreate(baseUri, href, out var absoluteUri))
                    {
                        // Only include links from same domain
                        if (absoluteUri.Host == baseUri.Host)
                        {
                            var normalizedUrl = $"{absoluteUri.Scheme}://{absoluteUri.Host}{absoluteUri.AbsolutePath}";
                            
                            // Exclude common patterns
                            if (!IsExcludedUrl(normalizedUrl))
                            {
                                links.Add(normalizedUrl);
                            }
                        }
                    }
                }
                catch
                {
                    // Skip invalid links
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract links from {Url}", baseUrl);
        }

        return links.Distinct().ToList();
    }

    private bool IsExcludedUrl(string url)
    {
        var lowerUrl = url.ToLower();
        
        // Exclude common non-design pages
        var excludePatterns = new[]
        {
            "/login", "/signup", "/sign-up", "/register",
            "/privacy", "/terms", "/legal", "/cookies",
            "/contact", "/support", "/help", "/faq",
            "/careers", "/jobs", "/press", "/media",
            "/api/", "/admin/", "/account/", "/settings/"
        };

        return excludePatterns.Any(pattern => lowerUrl.Contains(pattern));
    }

    private string SimplifyHtml(string html)
    {
        // Remove scripts
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<script\b[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Remove comments
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<!--[\s\S]*?-->", "");
        
        // Remove excessive whitespace
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");
        
        return html.Trim();
    }

    private string BuildLinkEvaluationPrompt(string baseUrl, List<string> links, int maxPages)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Website: {baseUrl}");
        sb.AppendLine($"Available links: {links.Count}");
        sb.AppendLine($"Select the {maxPages} most important pages to analyze for design quality.");
        sb.AppendLine();
        sb.AppendLine("Links:");
        
        for (int i = 0; i < Math.Min(links.Count, 50); i++) // Limit to 50 for prompt size
        {
            sb.AppendLine($"{i + 1}. {links[i]}");
        }
        
        sb.AppendLine();
        sb.AppendLine("Prioritize:");
        sb.AppendLine("1. Pricing page (most important for SaaS)");
        sb.AppendLine("2. Features/Product page");
        sb.AppendLine("3. About page");
        sb.AppendLine("4. Blog/Resources (if design-focused)");
        sb.AppendLine("5. Dashboard/App page (if accessible)");
        sb.AppendLine();
        sb.AppendLine("Return JSON array:");
        sb.AppendLine("[");
        sb.AppendLine("  {\"url\": \"...\", \"pageType\": \"pricing|features|about|blog|dashboard|generic\"},");
        sb.AppendLine("  ...");
        sb.AppendLine("]");

        return sb.ToString();
    }

    private string GetFallbackLinkEvaluationPrompt()
    {
        return @"You are a design analysis expert selecting the most important pages to analyze on a website.

Prioritize pages that showcase design quality:
1. **Pricing** - Most design effort in SaaS, critical page
2. **Features/Product** - Shows UI patterns, visual hierarchy
3. **About** - Shows brand identity, storytelling
4. **Blog** - Typography, content design (if not generic)
5. **Dashboard/App** - Actual product UI (if accessible)

Avoid: Login, signup, legal pages, contact forms, settings.

Return only the JSON array with selected pages.";
    }

    private Dictionary<string, string> ParseLinkSelection(string response, List<string> availableLinks)
    {
        var selected = new Dictionary<string, string>();

        try
        {
            // Extract JSON from response
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = JsonDocument.Parse(json);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("url", out var urlProp) &&
                        item.TryGetProperty("pageType", out var typeProp))
                    {
                        var url = urlProp.GetString();
                        var pageType = typeProp.GetString();

                        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(pageType))
                        {
                            // Find matching URL from available links (fuzzy match)
                            var matchingUrl = availableLinks.FirstOrDefault(link => 
                                link.Contains(url, StringComparison.OrdinalIgnoreCase) ||
                                url.Contains(link, StringComparison.OrdinalIgnoreCase));

                            if (matchingUrl != null)
                            {
                                selected[matchingUrl] = pageType;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse link selection, using fallback");
        }

        // Fallback: If LLM fails, select common patterns
        if (selected.Count == 0)
        {
            selected = FallbackLinkSelection(availableLinks);
        }

        return selected;
    }

    private Dictionary<string, string> FallbackLinkSelection(List<string> links)
    {
        var selected = new Dictionary<string, string>();

        // Pattern-based selection
        var patterns = new Dictionary<string, string>
        {
            { "pricing", "pricing" },
            { "features", "features" },
            { "product", "features" },
            { "about", "about" },
            { "blog", "blog" },
            { "dashboard", "dashboard" },
            { "app", "dashboard" }
        };

        foreach (var link in links)
        {
            var lowerLink = link.ToLower();
            
            foreach (var (pattern, pageType) in patterns)
            {
                if (lowerLink.Contains(pattern) && !selected.ContainsKey(link))
                {
                    selected[link] = pageType;
                    break;
                }
            }

            if (selected.Count >= 5)
                break;
        }

        return selected;
    }
}

