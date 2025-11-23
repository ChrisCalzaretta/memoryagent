namespace MemoryAgent.Server.Models;

/// <summary>
/// Response with pattern recommendations
/// </summary>
public class RecommendationResponse
{
    /// <summary>
    /// Project context that was analyzed
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Overall health score (0.0 to 1.0)
    /// </summary>
    public float OverallHealth { get; set; }

    /// <summary>
    /// Total patterns detected
    /// </summary>
    public int TotalPatternsDetected { get; set; }

    /// <summary>
    /// Recommendations
    /// </summary>
    public List<PatternRecommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// Analysis timestamp
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single pattern recommendation
/// </summary>
public class PatternRecommendation
{
    /// <summary>
    /// Priority level (CRITICAL, HIGH, MEDIUM, LOW)
    /// </summary>
    public string Priority { get; set; } = "MEDIUM";

    /// <summary>
    /// Pattern category
    /// </summary>
    public PatternCategory Category { get; set; }

    /// <summary>
    /// Pattern type
    /// </summary>
    public PatternType PatternType { get; set; }

    /// <summary>
    /// Issue description
    /// </summary>
    public string Issue { get; set; } = string.Empty;

    /// <summary>
    /// Recommendation text
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    /// Azure best practice URL
    /// </summary>
    public string? AzureUrl { get; set; }

    /// <summary>
    /// Affected files (if applicable)
    /// </summary>
    public List<string> AffectedFiles { get; set; } = new();

    /// <summary>
    /// Code example (optional)
    /// </summary>
    public string? CodeExample { get; set; }

    /// <summary>
    /// Impact description
    /// </summary>
    public string Impact { get; set; } = string.Empty;
}

