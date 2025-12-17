namespace DesignAgent.Server.Models.DesignIntelligence;

/// <summary>
/// Represents the analysis of a single page within a design
/// </summary>
public class PageAnalysis
{
    /// <summary>
    /// Unique identifier for this page analysis
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Parent design ID
    /// </summary>
    public required string DesignId { get; set; }
    
    /// <summary>
    /// Full URL of this page
    /// </summary>
    public required string Url { get; set; }
    
    /// <summary>
    /// Page type ("homepage", "pricing", "features", "dashboard", "blog", "generic")
    /// </summary>
    public required string PageType { get; set; }
    
    /// <summary>
    /// Screenshot paths for different breakpoints
    /// </summary>
    public ScreenshotSet Screenshots { get; set; } = new();
    
    /// <summary>
    /// Extracted DOM/HTML (simplified)
    /// </summary>
    public string? ExtractedHtml { get; set; }
    
    /// <summary>
    /// Extracted CSS (design-relevant portions)
    /// </summary>
    public string? ExtractedCss { get; set; }
    
    /// <summary>
    /// When this page was analyzed
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// LLM model used for analysis
    /// </summary>
    public string AnalysisModel { get; set; } = string.Empty;
    
    /// <summary>
    /// Category-level scores (e.g., { "hero": 9.2, "nav": 8.5, "socialProof": 9.0 })
    /// </summary>
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    
    /// <summary>
    /// Overall page score (weighted average of category scores)
    /// </summary>
    public double OverallPageScore { get; set; }
    
    /// <summary>
    /// Weight of this page in site aggregation (homepage=2.0, pricing=1.5, etc.)
    /// </summary>
    public double PageWeight { get; set; } = 1.0;
    
    /// <summary>
    /// Strengths identified by LLM
    /// </summary>
    public List<string> Strengths { get; set; } = new();
    
    /// <summary>
    /// Weaknesses identified by LLM
    /// </summary>
    public List<string> Weaknesses { get; set; } = new();
    
    /// <summary>
    /// Detailed category analysis (full JSON from LLM)
    /// </summary>
    public Dictionary<string, CategoryAnalysis> CategoryDetails { get; set; } = new();
    
    /// <summary>
    /// UX copy analysis (if applicable)
    /// </summary>
    public UxCopyAnalysis? CopyAnalysis { get; set; }
    
    /// <summary>
    /// Accessibility audit results
    /// </summary>
    public AccessibilityAudit? AccessibilityAudit { get; set; }
    
    /// <summary>
    /// Animation analysis (if video was captured)
    /// </summary>
    public AnimationAnalysis? AnimationAnalysis { get; set; }
}

/// <summary>
/// Screenshot paths for different responsive breakpoints
/// </summary>
public class ScreenshotSet
{
    /// <summary>
    /// Desktop screenshot path (1920px)
    /// </summary>
    public string? Desktop { get; set; }
    
    /// <summary>
    /// Tablet screenshot path (1024px)
    /// </summary>
    public string? Tablet { get; set; }
    
    /// <summary>
    /// Mobile screenshot path (375px)
    /// </summary>
    public string? Mobile { get; set; }
}

/// <summary>
/// Detailed analysis of a single category (e.g., hero section, navigation)
/// </summary>
public class CategoryAnalysis
{
    /// <summary>
    /// Category score (0-10)
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// Strengths specific to this category
    /// </summary>
    public List<string> Strengths { get; set; } = new();
    
    /// <summary>
    /// Weaknesses specific to this category
    /// </summary>
    public List<string> Weaknesses { get; set; } = new();
    
    /// <summary>
    /// Detailed analysis notes
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// UX copy analysis results
/// </summary>
public class UxCopyAnalysis
{
    /// <summary>
    /// Overall copy score (0-10)
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// Tone (e.g., "friendly", "professional", "playful", "technical")
    /// </summary>
    public string Tone { get; set; } = string.Empty;
    
    /// <summary>
    /// CTA (Call-to-Action) quality score (0-10)
    /// </summary>
    public double CtaQuality { get; set; }
    
    /// <summary>
    /// Value proposition clarity (0-10)
    /// </summary>
    public double ValuePropClarity { get; set; }
    
    /// <summary>
    /// Notable CTAs found
    /// </summary>
    public List<string> NotableCtAs { get; set; } = new();
    
    /// <summary>
    /// Microcopy examples
    /// </summary>
    public List<string> MicrocopyExamples { get; set; } = new();
}

/// <summary>
/// Accessibility audit results
/// </summary>
public class AccessibilityAudit
{
    /// <summary>
    /// Overall accessibility score (0-10)
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// WCAG level estimate ("A", "AA", "AAA", "Below A")
    /// </summary>
    public string WcagLevel { get; set; } = string.Empty;
    
    /// <summary>
    /// Color contrast issues found
    /// </summary>
    public int ContrastIssues { get; set; }
    
    /// <summary>
    /// Keyboard navigation quality (0-10)
    /// </summary>
    public double KeyboardNavigation { get; set; }
    
    /// <summary>
    /// Screen reader friendliness (0-10)
    /// </summary>
    public double ScreenReaderFriendly { get; set; }
    
    /// <summary>
    /// Issues found
    /// </summary>
    public List<string> Issues { get; set; } = new();
    
    /// <summary>
    /// Positive accessibility features
    /// </summary>
    public List<string> PositiveFeatures { get; set; } = new();
}

/// <summary>
/// Animation and motion design analysis
/// </summary>
public class AnimationAnalysis
{
    /// <summary>
    /// Overall animation quality score (0-10)
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// Animation style (e.g., "subtle", "bold", "none", "excessive")
    /// </summary>
    public string Style { get; set; } = string.Empty;
    
    /// <summary>
    /// Detected animation types (e.g., "parallax", "fade-in", "slide", "morph")
    /// </summary>
    public List<string> AnimationTypes { get; set; } = new();
    
    /// <summary>
    /// Scroll behavior (e.g., "smooth", "fixed-nav", "reveal-on-scroll")
    /// </summary>
    public string? ScrollBehavior { get; set; }
    
    /// <summary>
    /// Micro-interactions detected
    /// </summary>
    public List<string> MicroInteractions { get; set; } = new();
    
    /// <summary>
    /// Video capture path (if recorded)
    /// </summary>
    public string? VideoPath { get; set; }
}

