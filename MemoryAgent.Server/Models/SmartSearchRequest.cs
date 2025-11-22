namespace MemoryAgent.Server.Models;

/// <summary>
/// Request for smart search that auto-detects strategy
/// </summary>
public class SmartSearchRequest
{
    /// <summary>
    /// Natural language query or graph pattern
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Context to search within (optional)
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int Limit { get; set; } = 20;

    /// <summary>
    /// Offset for pagination (0 = first page)
    /// </summary>
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Include relationship data in results
    /// </summary>
    public bool IncludeRelationships { get; set; } = true;

    /// <summary>
    /// How many levels of relationships to include (1-3)
    /// </summary>
    public int RelationshipDepth { get; set; } = 1;

    /// <summary>
    /// Minimum semantic similarity score (0.0-1.0)
    /// </summary>
    public float MinimumScore { get; set; } = 0.5f;
}

