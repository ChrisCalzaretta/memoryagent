using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

public interface ITaskValidationService
{
    /// <summary>
    /// Validate a task against its rules before allowing completion
    /// </summary>
    Task<TaskValidationResult> ValidateTaskAsync(PlanTask task, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Auto-fix validation failures (create missing tests, files, etc.)
    /// </summary>
    Task<bool> AutoFixValidationFailuresAsync(PlanTask task, TaskValidationResult result, string context, CancellationToken cancellationToken = default);
}

public class TaskValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationFailure> Failures { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public class ValidationFailure
{
    public string RuleType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool CanAutoFix { get; set; }
    public string FixDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed actionable guidance for AI agents (like Cursor) to fix the issue
    /// </summary>
    public Dictionary<string, object> ActionableContext { get; set; } = new();
}

