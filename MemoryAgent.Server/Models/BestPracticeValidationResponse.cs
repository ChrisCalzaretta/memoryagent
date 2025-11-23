namespace MemoryAgent.Server.Models;

/// <summary>
/// Response from best practice validation
/// </summary>
public class BestPracticeValidationResponse
{
    /// <summary>
    /// Project context that was validated
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Overall compliance score (0.0 to 1.0)
    /// </summary>
    public float OverallScore { get; set; }

    /// <summary>
    /// Total number of practices checked
    /// </summary>
    public int TotalPracticesChecked { get; set; }

    /// <summary>
    /// Number of practices implemented
    /// </summary>
    public int PracticesImplemented { get; set; }

    /// <summary>
    /// Number of practices missing
    /// </summary>
    public int PracticesMissing { get; set; }

    /// <summary>
    /// Detailed results per practice
    /// </summary>
    public List<BestPracticeResult> Results { get; set; } = new();

    /// <summary>
    /// Validation timestamp
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result for a specific best practice
/// </summary>
public class BestPracticeResult
{
    /// <summary>
    /// Practice name (e.g., "caching", "retry-logic")
    /// </summary>
    public string Practice { get; set; } = string.Empty;

    /// <summary>
    /// Pattern type
    /// </summary>
    public PatternType PatternType { get; set; }

    /// <summary>
    /// Pattern category
    /// </summary>
    public PatternCategory Category { get; set; }

    /// <summary>
    /// Whether this practice is implemented
    /// </summary>
    public bool Implemented { get; set; }

    /// <summary>
    /// Number of instances found
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Average confidence score
    /// </summary>
    public float AverageConfidence { get; set; }

    /// <summary>
    /// Code examples (if requested)
    /// </summary>
    public List<PatternExample> Examples { get; set; } = new();

    /// <summary>
    /// Recommendation if not implemented
    /// </summary>
    public string? Recommendation { get; set; }

    /// <summary>
    /// Azure best practice URL
    /// </summary>
    public string? AzureUrl { get; set; }
}

/// <summary>
/// Example of a detected pattern
/// </summary>
public class PatternExample
{
    /// <summary>
    /// File path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Implementation details
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// Code snippet
    /// </summary>
    public string CodeSnippet { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score
    /// </summary>
    public float Confidence { get; set; }
}

