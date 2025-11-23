namespace MemoryAgent.Server.Models;

/// <summary>
/// Request for pattern recommendations
/// </summary>
public class RecommendationRequest
{
    /// <summary>
    /// Project context to analyze
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Focus on specific categories (optional)
    /// </summary>
    public List<PatternCategory>? Categories { get; set; }

    /// <summary>
    /// Include low-priority recommendations
    /// </summary>
    public bool IncludeLowPriority { get; set; } = false;

    /// <summary>
    /// Maximum number of recommendations to return
    /// </summary>
    public int MaxRecommendations { get; set; } = 10;
}

