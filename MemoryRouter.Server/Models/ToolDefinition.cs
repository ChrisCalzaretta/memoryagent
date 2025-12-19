namespace MemoryRouter.Server.Models;

/// <summary>
/// MCP tool definition as returned by servers' tools/list endpoint
/// </summary>
public class McpToolDefinition
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object>? InputSchema { get; set; }
}

/// <summary>
/// Represents a tool that can be called by the router (augmented with orchestration metadata)
/// </summary>
public class ToolDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Service { get; set; } // "memory-agent" or "coding-orchestrator"
    public required Dictionary<string, object> InputSchema { get; set; }
    public List<string> UseCases { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
}

/// <summary>
/// Google's function calling format (what FunctionGemma is trained on)
/// </summary>
public class GoogleFunctionCall
{
    public required string Name { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// FunctionGemma's plan for handling a request
/// </summary>
public class WorkflowPlan
{
    public string Reasoning { get; set; } = string.Empty;
    public List<FunctionCall> FunctionCalls { get; set; } = new();
}

/// <summary>
/// A single function call in a workflow
/// </summary>
public class FunctionCall
{
    public required string Name { get; set; }
    public required Dictionary<string, object> Arguments { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public int Order { get; set; }
}

/// <summary>
/// Result of executing a workflow step
/// </summary>
public class StepResult
{
    public required string ToolName { get; set; }
    public required bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Complete workflow execution result
/// </summary>
public class WorkflowResult
{
    public required string RequestId { get; set; }
    public required string OriginalRequest { get; set; }
    public required WorkflowPlan Plan { get; set; }
    public required List<StepResult> Steps { get; set; }
    public required bool Success { get; set; }
    public string? FinalResult { get; set; }
    public string? Error { get; set; }
    public long TotalDurationMs { get; set; }
}

