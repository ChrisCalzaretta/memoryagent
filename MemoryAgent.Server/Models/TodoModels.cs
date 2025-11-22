namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a TODO item in the codebase
/// </summary>
public class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Context { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Pending;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum TodoPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TodoStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// Represents a development plan with multiple tasks
/// </summary>
public class DevelopmentPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlanStatus Status { get; set; } = PlanStatus.Active;
    public List<PlanTask> Tasks { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class PlanTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public int OrderIndex { get; set; }
    public List<string> Dependencies { get; set; } = new(); // Task IDs that must complete first
    public DateTime? CompletedAt { get; set; }
}

public enum PlanStatus
{
    Draft,
    Active,
    Completed,
    Cancelled,
    OnHold
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Blocked,
    Completed,
    Cancelled
}

// Request/Response models
public class AddTodoRequest
{
    public string Context { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
}

public class RemoveTodoRequest
{
    public string TodoId { get; set; } = string.Empty;
}

public class AddPlanRequest
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PlanTaskRequest> Tasks { get; set; } = new();
}

public class PlanTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

public class UpdatePlanRequest
{
    public string PlanId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PlanStatus? Status { get; set; }
    public List<UpdatePlanTaskRequest>? Tasks { get; set; }
}

public class UpdatePlanTaskRequest
{
    public string TaskId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TaskStatus? Status { get; set; }
}

public class CompletePlanRequest
{
    public string PlanId { get; set; } = string.Empty;
}

public class PlanStatusRequest
{
    public string? Context { get; set; }
    public PlanStatus? Status { get; set; }
}

