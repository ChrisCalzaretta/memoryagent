namespace DesignAgent.Server.Models.DesignIntelligence;

/// <summary>
/// Represents a learned design pattern extracted from analyzed designs
/// </summary>
public class DesignPattern
{
    /// <summary>
    /// Unique identifier for this pattern
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Pattern name (e.g., "3-Tier Pricing Card", "Hero with Gradient Background")
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Pattern category (e.g., "hero", "pricing", "navigation", "form", "footer")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Pattern type (e.g., "layout", "component", "interaction", "typography")
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the pattern
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Quality score of this pattern (0-10)
    /// </summary>
    public double QualityScore { get; set; }
    
    /// <summary>
    /// How many times this pattern was observed
    /// </summary>
    public int ObservationCount { get; set; } = 1;
    
    /// <summary>
    /// Which designs this pattern was extracted from
    /// </summary>
    public List<string> SourceDesignIds { get; set; } = new();
    
    /// <summary>
    /// HTML structure of this pattern (simplified)
    /// </summary>
    public string? HtmlStructure { get; set; }
    
    /// <summary>
    /// CSS styling for this pattern
    /// </summary>
    public string? CssStyle { get; set; }
    
    /// <summary>
    /// A2UI representation of this pattern
    /// </summary>
    public string? A2uiJson { get; set; }
    
    /// <summary>
    /// Design tokens used in this pattern
    /// </summary>
    public Dictionary<string, string> DesignTokens { get; set; } = new();
    
    /// <summary>
    /// Tags for this pattern (e.g., ["gradient", "3-column", "responsive"])
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// When this pattern was first learned
    /// </summary>
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this pattern was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Patterns that frequently co-occur with this one
    /// </summary>
    public Dictionary<string, int> CoOccurringPatterns { get; set; } = new();
}

/// <summary>
/// Represents user feedback on a design
/// </summary>
public class DesignFeedback
{
    /// <summary>
    /// Unique identifier for this feedback
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Design ID this feedback is for
    /// </summary>
    public required string DesignId { get; set; }
    
    /// <summary>
    /// Rating (1 = thumbs down, 5 = thumbs up)
    /// </summary>
    public int Rating { get; set; }
    
    /// <summary>
    /// Mapped human score (1→4.0, 5→9.0)
    /// </summary>
    public double HumanScore { get; set; }
    
    /// <summary>
    /// LLM score at the time of feedback
    /// </summary>
    public double LlmScore { get; set; }
    
    /// <summary>
    /// Mismatch between human and LLM (absolute difference)
    /// </summary>
    public double Mismatch { get; set; }
    
    /// <summary>
    /// Custom name provided by user (optional)
    /// </summary>
    public string? CustomName { get; set; }
    
    /// <summary>
    /// When feedback was provided
    /// </summary>
    public DateTime ProvidedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this feedback triggered prompt evolution
    /// </summary>
    public bool TriggeredEvolution { get; set; }
}

/// <summary>
/// Tracks performance of LLM models for calibration
/// </summary>
public class ModelPerformance
{
    /// <summary>
    /// Model name (e.g., "llava:13b")
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// Page type this performance data is for
    /// </summary>
    public required string PageType { get; set; }
    
    /// <summary>
    /// Average bias (LLM - Human, negative = underscores)
    /// </summary>
    public double AverageBias { get; set; }
    
    /// <summary>
    /// Standard deviation of bias
    /// </summary>
    public double StandardDeviation { get; set; }
    
    /// <summary>
    /// Accuracy (correlation coefficient 0-1)
    /// </summary>
    public double Accuracy { get; set; }
    
    /// <summary>
    /// Number of comparisons used for calibration
    /// </summary>
    public int SampleSize { get; set; }
    
    /// <summary>
    /// When this calibration was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Apply calibration to an LLM score
    /// </summary>
    public double CalibrateScore(double llmScore)
    {
        // Simple bias correction
        var calibrated = llmScore - AverageBias;

        // Clamp to 0-10 range
        return Math.Max(0, Math.Min(10, calibrated));
    }
}

