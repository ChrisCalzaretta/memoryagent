namespace ValidationAgent.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server (Lightning)
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get the active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Get validation rules and patterns from Lightning
    /// </summary>
    Task<List<ValidationRuleInfo>> GetValidationRulesAsync(string context, CancellationToken cancellationToken);

    /// <summary>
    /// Record feedback on prompt performance
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken);

    /// <summary>
    /// Record pattern feedback (was the pattern useful?)
    /// </summary>
    Task RecordPatternFeedbackAsync(string patternName, bool wasUseful, string? comments, CancellationToken cancellationToken);

    /// <summary>
    /// Check if MemoryAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Record model performance for future selection
    /// </summary>
    Task RecordModelPerformanceAsync(
        string model,
        string taskType,
        bool succeeded,
        double score,
        string? language = null,
        string? complexity = null,
        int iterations = 1,
        long durationMs = 0,
        string? errorType = null,
        List<string>? taskKeywords = null,
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Get historical stats for models
    /// </summary>
    Task<List<ModelPerformanceStats>> GetModelStatsAsync(
        string? taskType,
        string? language,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Model performance statistics for selection
/// </summary>
public class ModelPerformanceStats
{
    public string Model { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string Language { get; set; } = "";
    public int TotalAttempts { get; set; }
    public int Successes { get; set; }
    public double SuccessRate => TotalAttempts > 0 ? (double)Successes / TotalAttempts * 100 : 0;
    public double AverageScore { get; set; }
}

/// <summary>
/// Prompt information from Lightning
/// </summary>
public class PromptInfo
{
    public required string Name { get; set; }
    public required string Content { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Validation rule from Lightning patterns
/// </summary>
public class ValidationRuleInfo
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string Severity { get; set; } = "medium";
    public string? CheckPattern { get; set; }
    public string? FixSuggestion { get; set; }
}



