namespace MemoryAgent.Server.Models;

/// <summary>
/// Response from smart search
/// </summary>
public class SmartSearchResponse
{
    /// <summary>
    /// Original query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Strategy used (graph-first, semantic-first, hybrid)
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// Search results
    /// </summary>
    public List<SmartSearchResult> Results { get; set; } = new();

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalFound { get; set; }

    /// <summary>
    /// Whether there are more results available
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// How long the search took
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Metadata about the search
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Individual search result with enriched data
/// </summary>
public class SmartSearchResult
{
    /// <summary>
    /// Element name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Element type (Class, Method, File, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Code content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// File path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number in file
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Combined relevance score (0.0-1.0)
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Semantic similarity score (0.0-1.0)
    /// </summary>
    public float SemanticScore { get; set; }

    /// <summary>
    /// Graph relevance score (0.0-1.0)
    /// </summary>
    public float GraphScore { get; set; }

    /// <summary>
    /// Relationships if requested
    /// </summary>
    public Dictionary<string, List<string>>? Relationships { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

