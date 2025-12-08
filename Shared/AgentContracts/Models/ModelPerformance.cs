namespace AgentContracts.Models;

/// <summary>
/// Records a model's performance on a specific task for learning
/// </summary>
public class ModelPerformanceRecord
{
    /// <summary>
    /// Model name (e.g., "qwen2.5-coder:14b")
    /// </summary>
    public string Model { get; set; } = "";
    
    /// <summary>
    /// Task type: code_generation, fix, validation, complexity_estimation
    /// </summary>
    public string TaskType { get; set; } = "";
    
    /// <summary>
    /// Programming language: csharp, python, typescript, flutter, etc.
    /// </summary>
    public string Language { get; set; } = "";
    
    /// <summary>
    /// Task complexity: simple, moderate, complex, very_complex
    /// </summary>
    public string Complexity { get; set; } = "";
    
    /// <summary>
    /// Outcome: success, partial, failure
    /// </summary>
    public string Outcome { get; set; } = "";
    
    /// <summary>
    /// Validation score (0-10)
    /// </summary>
    public int Score { get; set; }
    
    /// <summary>
    /// Time taken in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// Number of iterations needed (1 = first try success)
    /// </summary>
    public int Iterations { get; set; } = 1;
    
    /// <summary>
    /// Keywords extracted from task description
    /// </summary>
    public List<string> TaskKeywords { get; set; } = new();
    
    /// <summary>
    /// Error type if failed: build_failure, runtime_error, validation_fail, timeout
    /// </summary>
    public string? ErrorType { get; set; }
    
    /// <summary>
    /// Project context for scoped learning
    /// </summary>
    public string Context { get; set; } = "";
    
    /// <summary>
    /// Timestamp when this was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to query the best model for a task
/// </summary>
public class BestModelRequest
{
    /// <summary>
    /// Task type: code_generation, fix, validation
    /// </summary>
    public string TaskType { get; set; } = "code_generation";
    
    /// <summary>
    /// Programming language
    /// </summary>
    public string Language { get; set; } = "";
    
    /// <summary>
    /// Task complexity
    /// </summary>
    public string Complexity { get; set; } = "";
    
    /// <summary>
    /// Keywords from task description
    /// </summary>
    public List<string> TaskKeywords { get; set; } = new();
    
    /// <summary>
    /// Project context
    /// </summary>
    public string Context { get; set; } = "";
    
    /// <summary>
    /// Models to exclude (already tried)
    /// </summary>
    public List<string> ExcludeModels { get; set; } = new();
    
    /// <summary>
    /// Maximum VRAM available (GB)
    /// </summary>
    public double MaxVramGb { get; set; } = 24;
}

/// <summary>
/// Response with recommended models
/// </summary>
public class BestModelResponse
{
    /// <summary>
    /// Recommended model (best match)
    /// </summary>
    public string RecommendedModel { get; set; } = "";
    
    /// <summary>
    /// Why this model was recommended
    /// </summary>
    public string Reasoning { get; set; } = "";
    
    /// <summary>
    /// Historical success rate for similar tasks (0-100%)
    /// </summary>
    public double SuccessRate { get; set; }
    
    /// <summary>
    /// Average score on similar tasks
    /// </summary>
    public double AverageScore { get; set; }
    
    /// <summary>
    /// Number of similar tasks this model has done
    /// </summary>
    public int SampleCount { get; set; }
    
    /// <summary>
    /// Alternative models ranked by performance
    /// </summary>
    public List<ModelRecommendation> Alternatives { get; set; } = new();
    
    /// <summary>
    /// Whether this is based on historical data or LLM estimation
    /// </summary>
    public bool IsHistorical { get; set; }
}

/// <summary>
/// A model recommendation with stats
/// </summary>
public class ModelRecommendation
{
    public string Model { get; set; } = "";
    public double SuccessRate { get; set; }
    public double AverageScore { get; set; }
    public int SampleCount { get; set; }
    public double SizeGb { get; set; }
    public string Reasoning { get; set; } = "";
}

/// <summary>
/// Aggregated stats for a model on a task type
/// </summary>
public class ModelStats
{
    public string Model { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string Language { get; set; } = "";
    public int TotalAttempts { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public double SuccessRate => TotalAttempts > 0 ? (double)Successes / TotalAttempts * 100 : 0;
    public double AverageScore { get; set; }
    public double AverageDurationMs { get; set; }
    public double AverageIterations { get; set; }
}

