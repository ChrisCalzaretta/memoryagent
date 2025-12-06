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
    TimedOut
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
}



