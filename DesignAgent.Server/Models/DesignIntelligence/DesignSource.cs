namespace DesignAgent.Server.Models.DesignIntelligence;

/// <summary>
/// Represents a source website discovered by the system for design analysis
/// </summary>
public class DesignSource
{
    /// <summary>
    /// Unique identifier for the design source
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// URL of the website (e.g., "https://linear.app")
    /// </summary>
    public required string Url { get; set; }
    
    /// <summary>
    /// Category of the design (e.g., "saas", "ecommerce", "portfolio")
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Trust score (0-10) indicating reliability of the source
    /// Higher trust = lower quality threshold
    /// Examples: Awwwards=10, Dribbble=7, Random site=5
    /// </summary>
    public double TrustScore { get; set; } = 5.0;
    
    /// <summary>
    /// How this source was discovered ("curated", "google_search", "bing_search", "related_link")
    /// </summary>
    public string DiscoveryMethod { get; set; } = "unknown";
    
    /// <summary>
    /// Search query that led to this source (if discovered via search)
    /// </summary>
    public string? DiscoveryQuery { get; set; }
    
    /// <summary>
    /// When this source was discovered
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this source was last crawled/analyzed
    /// </summary>
    public DateTime? LastCrawledAt { get; set; }
    
    /// <summary>
    /// Status of this source ("pending", "processing", "analyzed", "failed", "discarded")
    /// </summary>
    public string Status { get; set; } = "pending";
    
    /// <summary>
    /// Tags associated with this source (e.g., ["minimal", "gradient", "dark-mode"])
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Whether this source has already been evaluated (prevents re-crawling)
    /// </summary>
    public bool AlreadyEvaluated { get; set; } = false;
    
    /// <summary>
    /// Final score if analyzed (0-10)
    /// </summary>
    public double? FinalScore { get; set; }
    
    /// <summary>
    /// Reason for discard if below threshold
    /// </summary>
    public string? DiscardReason { get; set; }
}

