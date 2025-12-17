using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for capturing design screenshots and DOM
/// </summary>
public interface IDesignCaptureService
{
    /// <summary>
    /// Crawl a website and capture all important pages
    /// </summary>
    /// <param name="source">Design source to crawl</param>
    /// <returns>Captured design with all pages</returns>
    Task<CapturedDesign> CrawlWebsiteAsync(DesignSource source, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Capture a single page (screenshots + DOM)
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <param name="pageType">Page type (homepage, pricing, etc.)</param>
    /// <returns>Page analysis with screenshots</returns>
    Task<PageAnalysis> CapturePageAsync(string url, string pageType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Use LLM to evaluate which links on a page are worth crawling
    /// </summary>
    /// <param name="baseUrl">Base website URL</param>
    /// <param name="links">All links found on homepage</param>
    /// <param name="maxPages">Max pages to select</param>
    /// <returns>Selected links with page types</returns>
    Task<Dictionary<string, string>> SelectImportantLinksAsync(string baseUrl, List<string> links, int maxPages = 6, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Take screenshots at multiple breakpoints
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <param name="breakpoints">Breakpoint widths (e.g., [1920, 1024, 375])</param>
    /// <returns>Screenshot file paths</returns>
    Task<ScreenshotSet> TakeScreenshotsAsync(string url, int[] breakpoints, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract DOM/HTML from page
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <returns>Simplified HTML structure</returns>
    Task<string> ExtractDomAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract CSS from page (design-relevant portions)
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <returns>Extracted CSS</returns>
    Task<string> ExtractCssAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Capture video for animation analysis (optional)
    /// </summary>
    /// <param name="url">Page URL</param>
    /// <param name="durationSec">Recording duration</param>
    /// <returns>Video file path</returns>
    Task<string?> CaptureVideoAsync(string url, int durationSec = 10, CancellationToken cancellationToken = default);
}

