namespace AgentContracts.Responses;

/// <summary>
/// Response for task status queries
/// </summary>
public class TaskStatusResponse
{
    /// <summary>
    /// The job ID
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public required TaskState Status { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Current phase description
    /// </summary>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Current iteration number
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Maximum iterations allowed
    /// </summary>
    public int MaxIterations { get; set; }

    /// <summary>
    /// Timeline of completed phases
    /// </summary>
    public List<PhaseInfo> Timeline { get; set; } = new();

    /// <summary>
    /// Final result (when complete)
    /// </summary>
    public TaskResult? Result { get; set; }

    /// <summary>
    /// Error details (when failed)
    /// </summary>
    public TaskError? Error { get; set; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// The original task description
    /// </summary>
    public string? Task { get; set; }
    
    /// <summary>
    /// Execution plan with checklist (populated after plan generation phase)
    /// </summary>
    public TaskPlanInfo? Plan { get; set; }
    
    /// <summary>
    /// Files generated so far (even if incomplete)
    /// </summary>
    public List<string> GeneratedFiles { get; set; } = new();
    
    /// <summary>
    /// Cloud LLM usage statistics (when using Anthropic/OpenAI)
    /// </summary>
    public CloudUsage? CloudUsage { get; set; }
}

/// <summary>
/// Cloud LLM usage tracking for cost monitoring
/// </summary>
public class CloudUsage
{
    /// <summary>
    /// Provider name (e.g., "anthropic", "openai")
    /// </summary>
    public string? Provider { get; set; }
    
    /// <summary>
    /// Model used (e.g., "claude-sonnet-4-20250514")
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Total input tokens used this task
    /// </summary>
    public int InputTokens { get; set; }
    
    /// <summary>
    /// Total output tokens used this task
    /// </summary>
    public int OutputTokens { get; set; }
    
    /// <summary>
    /// Number of API calls made
    /// </summary>
    public int ApiCalls { get; set; }
    
    /// <summary>
    /// Estimated cost in USD
    /// </summary>
    public decimal EstimatedCost { get; set; }
    
    /// <summary>
    /// Tokens remaining (from rate limit headers)
    /// </summary>
    public int? TokensRemaining { get; set; }
    
    /// <summary>
    /// Requests remaining (from rate limit headers)
    /// </summary>
    public int? RequestsRemaining { get; set; }
    
    /// <summary>
    /// Note about balance checking
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// Execution plan info for status display
/// </summary>
public class TaskPlanInfo
{
    /// <summary>
    /// Required classes/components to generate
    /// </summary>
    public List<string> RequiredClasses { get; set; } = new();
    
    /// <summary>
    /// Order to generate files (dependencies first)
    /// </summary>
    public List<string> DependencyOrder { get; set; } = new();
    
    /// <summary>
    /// Semantic breakdown of the task
    /// </summary>
    public string SemanticBreakdown { get; set; } = "";
    
    /// <summary>
    /// Individual steps with their status
    /// </summary>
    public List<PlanStepInfo> Steps { get; set; } = new();
}

/// <summary>
/// A step in the plan with its status
/// </summary>
public class PlanStepInfo
{
    public int Order { get; set; }
    public string Description { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Status { get; set; } = "pending";  // pending, in_progress, completed, failed
}

/// <summary>
/// Task states
/// </summary>
public enum TaskState
{
    Queued,
    Running,
    Complete,
    Failed,
    Cancelled,
    TimedOut,
    /// <summary>
    /// Step-by-step mode: A step failed after max retries, waiting for user help
    /// User can provide feedback via resume endpoint to continue
    /// </summary>
    NeedsHelp
}

/// <summary>
/// Information about a completed phase
/// </summary>
public class PhaseInfo
{
    public required string Name { get; set; }
    public int? Iteration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string Status { get; set; } = "complete";
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Final result of a completed task
/// </summary>
public class TaskResult
{
    public bool Success { get; set; }
    public List<GeneratedFile> Files { get; set; } = new();
    public int ValidationScore { get; set; }
    public int TotalIterations { get; set; }
    public long TotalDurationMs { get; set; }
    public string? Summary { get; set; }
    
    /// <summary>
    /// LLM models used during the task (e.g., "claude:claude-sonnet-4-20250514", "phi4:latest")
    /// </summary>
    public List<string> ModelsUsed { get; set; } = new();
}

/// <summary>
/// A file generated/modified by the task
/// </summary>
public class GeneratedFile
{
    public required string Path { get; set; }
    public required string Content { get; set; }
    public FileChangeType ChangeType { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Type of file change
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted
}

/// <summary>
/// Error details when task fails
/// </summary>
public class TaskError
{
    public required string Type { get; set; }
    public required string Message { get; set; }
    public string? Phase { get; set; }
    public TaskResult? PartialResult { get; set; }
    public bool CanRetry { get; set; }
    
    /// <summary>
    /// Rich error details (for NeedsHelp state: step info, specific errors, help examples)
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}





