namespace DesignAgent.Server.Models.DesignIntelligence;

/// <summary>
/// Configuration options for the Design Intelligence System
/// </summary>
public class DesignIntelligenceOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "DesignIntelligence";
    
    // Quality & Leaderboard
    
    /// <summary>
    /// Initial quality threshold (0-10 scale)
    /// Only designs scoring >= this value are kept
    /// Default: 7.0
    /// </summary>
    public double InitialThreshold { get; set; } = 7.0;
    
    /// <summary>
    /// Maximum number of designs to keep in leaderboard
    /// Default: 100
    /// </summary>
    public int LeaderboardSize { get; set; } = 100;
    
    // Crawling
    
    /// <summary>
    /// Maximum pages to crawl per site (1-10)
    /// Default: 6
    /// </summary>
    public int MaxPagesPerSite { get; set; } = 6;
    
    /// <summary>
    /// Delay between page crawls (milliseconds)
    /// Default: 2000 (2 seconds)
    /// </summary>
    public int CrawlDelayMs { get; set; } = 2000;
    
    /// <summary>
    /// Playwright timeout (milliseconds)
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int PlaywrightTimeoutMs { get; set; } = 30000;
    
    // Screenshots & Media
    
    /// <summary>
    /// Screenshot storage path
    /// Default: "./data/screenshots"
    /// </summary>
    public string ScreenshotPath { get; set; } = "./data/screenshots";
    
    /// <summary>
    /// Screenshot quality (0-100)
    /// Default: 85
    /// </summary>
    public int ScreenshotQuality { get; set; } = 85;
    
    /// <summary>
    /// Screenshot breakpoints (widths in pixels)
    /// Default: [1920, 1024, 375]
    /// </summary>
    public int[] ScreenshotBreakpoints { get; set; } = { 1920, 1024, 375 };
    
    /// <summary>
    /// Enable video capture for animation analysis
    /// Default: false (disabled for now)
    /// </summary>
    public bool EnableVideoCapture { get; set; } = false;
    
    /// <summary>
    /// Video capture duration (seconds)
    /// Default: 10
    /// </summary>
    public int VideoCaptureDurationSec { get; set; } = 10;
    
    // Discovery
    
    /// <summary>
    /// Primary search API provider ("google", "bing", "brave", "duckduckgo", "serper")
    /// Will automatically fallback to other providers if this one fails
    /// Default: "google"
    /// </summary>
    public string SearchProvider { get; set; } = "google";
    
    /// <summary>
    /// Google Search API key (for Google Custom Search)
    /// </summary>
    public string? SearchApiKey { get; set; }
    
    /// <summary>
    /// Google Custom Search Engine ID (required for Google search)
    /// </summary>
    public string? SearchEngineId { get; set; }
    
    /// <summary>
    /// Bing Search API key (optional - for Bing fallback)
    /// Get from: https://portal.azure.com (Bing Search v7)
    /// Free tier: 3,000 queries/month
    /// </summary>
    public string? BingApiKey { get; set; }
    
    /// <summary>
    /// Brave Search API key (optional - for Brave fallback)
    /// Get from: https://brave.com/search/api/
    /// Free tier: 2,000 queries/month
    /// </summary>
    public string? BraveApiKey { get; set; }
    
    /// <summary>
    /// Serper API key (optional - for Serper fallback)
    /// Get from: https://serper.dev
    /// Paid only: $50/month for 5,000 queries (no free tier)
    /// </summary>
    public string? SerperApiKey { get; set; }
    
    /// <summary>
    /// Number of search queries per discovery run
    /// Default: 5
    /// </summary>
    public int SearchQueriesPerRun { get; set; } = 5;
    
    /// <summary>
    /// Results to fetch per search query
    /// Default: 10
    /// </summary>
    public int SearchResultsPerQuery { get; set; } = 10;
    
    // Background Service
    
    /// <summary>
    /// Enable background learning service
    /// Default: true
    /// </summary>
    public bool EnableBackgroundLearning { get; set; } = true;
    
    /// <summary>
    /// Background service interval (seconds)
    /// Default: 3600 (1 hour)
    /// </summary>
    public int BackgroundIntervalSec { get; set; } = 3600;
    
    /// <summary>
    /// Maximum CPU usage percentage for background service
    /// Default: 30
    /// </summary>
    public int MaxCpuPercent { get; set; } = 30;
    
    // Prompt Evolution
    
    /// <summary>
    /// Minimum feedback items before prompt evolution
    /// Default: 10
    /// </summary>
    public int MinFeedbackForEvolution { get; set; } = 10;
    
    /// <summary>
    /// Mismatch threshold to trigger evolution analysis
    /// Default: 2.0 (points difference)
    /// </summary>
    public double MismatchThreshold { get; set; } = 2.0;
    
    // LLM Configuration
    
    /// <summary>
    /// Default LLM for vision analysis
    /// Default: "llava:13b"
    /// </summary>
    public string VisionModel { get; set; } = "llava:13b";
    
    /// <summary>
    /// Default LLM for text analysis
    /// Default: "phi4"
    /// </summary>
    public string TextModel { get; set; } = "phi4";
    
    /// <summary>
    /// Timeout for LLM calls (seconds)
    /// Default: 120
    /// </summary>
    public int LlmTimeoutSec { get; set; } = 120;
}

