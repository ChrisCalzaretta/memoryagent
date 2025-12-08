using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get context for a task (similar questions, patterns, etc.)
    /// </summary>
    Task<CodeContext?> GetContextAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Store a successful Q&A for future recall
    /// </summary>
    Task StoreQaAsync(string question, string answer, List<string> relevantFiles, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Get active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Record feedback on prompt performance
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken);

    /// <summary>
    /// Check if MemoryAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 TASK LEARNING: Record detailed task failure for future avoidance
    /// </summary>
    Task RecordTaskFailureAsync(TaskFailureRecord failure, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 TASK LEARNING: Query lessons learned from similar failed tasks
    /// </summary>
    Task<TaskLessonsResult> QueryTaskLessonsAsync(string taskDescription, List<string> keywords, string language, CancellationToken cancellationToken);
}

/// <summary>
/// Record of a failed task for learning
/// </summary>
public class TaskFailureRecord
{
    public required string TaskDescription { get; set; }
    public List<string> TaskKeywords { get; set; } = new();
    public required string Language { get; set; }
    public required string FailurePhase { get; set; }  // code_generation, validation, docker_build, docker_run
    public required string ErrorMessage { get; set; }
    public string ErrorPattern { get; set; } = "unknown";  // Categorized error type
    public List<string> ApproachesTried { get; set; } = new();
    public List<string> ModelsUsed { get; set; } = new();
    public int IterationsAttempted { get; set; }
    public string LessonsLearned { get; set; } = "";
    public string Context { get; set; } = "default";
}

/// <summary>
/// Result of querying task lessons
/// </summary>
public class TaskLessonsResult
{
    public int FoundLessons { get; set; }
    public string AvoidanceAdvice { get; set; } = "";
    public List<string> SuggestedApproaches { get; set; } = new();
    public List<TaskLesson> Lessons { get; set; } = new();
}

/// <summary>
/// A single lesson learned from a past failure
/// </summary>
public class TaskLesson
{
    public string TaskDescription { get; set; } = "";
    public string Language { get; set; } = "";
    public string FailurePhase { get; set; } = "";
    public string ErrorPattern { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public List<string> ApproachesTried { get; set; } = new();
    public string LessonsLearned { get; set; } = "";
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

