using System.Text.Json;
using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Handles MCP tool calls for the orchestrator
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<McpHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpHandler(IJobManager jobManager, ILogger<McpHandler> logger)
    {
        _jobManager = jobManager;
        _logger = logger;
    }

    public IEnumerable<object> GetToolDefinitions()
    {
        var tools = new List<object>
        {
            new Dictionary<string, object>
            {
                ["name"] = "orchestrate_task",
                ["description"] = "Start a multi-agent coding task. The coding agent generates code and the validation agent reviews it until quality standards are met.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["task"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The coding task to perform (e.g., 'Add caching to UserService')" },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Project context name for Lightning memory" },
                        ["workspacePath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Path to the workspace root" },
                        ["background"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Run as background job (default: true)", ["default"] = true },
                        ["maxIterations"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Maximum coding/validation iterations (default: 5)", ["default"] = 5 },
                        ["minValidationScore"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Minimum score to pass validation (default: 8)", ["default"] = 8 }
                    },
                    ["required"] = new[] { "task", "context", "workspacePath" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "get_task_status",
                ["description"] = "Get the status of a running or completed coding task",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["jobId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The job ID returned by orchestrate_task" }
                    },
                    ["required"] = new[] { "jobId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "cancel_task",
                ["description"] = "Cancel a running coding task",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["jobId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The job ID to cancel" }
                    },
                    ["required"] = new[] { "jobId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "list_tasks",
                ["description"] = "List all active and recent coding tasks",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            }
        };
        return tools;
    }

    public async Task<string> HandleToolCallAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling MCP tool call: {Tool}", toolName);

        return toolName switch
        {
            "orchestrate_task" => await HandleOrchestrateTaskAsync(arguments, cancellationToken),
            "get_task_status" => HandleGetTaskStatus(arguments),
            "cancel_task" => HandleCancelTask(arguments),
            "list_tasks" => HandleListTasks(),
            _ => $"Unknown tool: {toolName}"
        };
    }

    private async Task<string> HandleOrchestrateTaskAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var request = new OrchestrateTaskRequest
        {
            Task = GetStringArg(arguments, "task"),
            Context = GetStringArg(arguments, "context"),
            WorkspacePath = GetStringArg(arguments, "workspacePath"),
            Background = GetBoolArg(arguments, "background", true),
            MaxIterations = GetIntArg(arguments, "maxIterations", 5),
            MinValidationScore = GetIntArg(arguments, "minValidationScore", 8)
        };

        var jobId = await _jobManager.StartJobAsync(request, cancellationToken);

        return $@"🚀 **Multi-Agent Coding Task Started**

**Job ID:** `{jobId}`
**Task:** {request.Task}
**Context:** {request.Context}

The coding agent and validation agent are now working on your task. 

**To check status:** Call `get_task_status` with jobId: `{jobId}`

**Progress will include:**
- Context gathering from Lightning memory
- Code generation iterations
- Validation passes with scores
- Final result with generated files";
    }

    private string HandleGetTaskStatus(Dictionary<string, object> arguments)
    {
        var jobId = GetStringArg(arguments, "jobId");
        var status = _jobManager.GetJobStatus(jobId);

        if (status == null)
        {
            return $"❌ Job `{jobId}` not found";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine($"📊 **Task Status: {status.Status}**");
        result.AppendLine();
        result.AppendLine($"**Job ID:** `{status.JobId}`");
        result.AppendLine($"**Progress:** {status.Progress}%");
        result.AppendLine($"**Current Phase:** {status.CurrentPhase}");
        result.AppendLine($"**Iteration:** {status.Iteration}/{status.MaxIterations}");
        result.AppendLine();

        if (status.Timeline.Any())
        {
            result.AppendLine("**Timeline:**");
            foreach (var phase in status.Timeline)
            {
                var duration = phase.DurationMs.HasValue ? $" ({phase.DurationMs}ms)" : "";
                var iterInfo = phase.Iteration.HasValue ? $" [iter {phase.Iteration}]" : "";
                result.AppendLine($"- ✅ {phase.Name}{iterInfo}{duration}");
            }
            result.AppendLine();
        }

        if (status.Status == AgentContracts.Responses.TaskState.Complete && status.Result != null)
        {
            result.AppendLine("**✅ COMPLETED**");
            result.AppendLine($"- Validation Score: {status.Result.ValidationScore}/10");
            result.AppendLine($"- Total Iterations: {status.Result.TotalIterations}");
            result.AppendLine($"- Duration: {status.Result.TotalDurationMs}ms");
            result.AppendLine($"- Files Generated: {status.Result.Files.Count}");
            result.AppendLine();
            
            foreach (var file in status.Result.Files)
            {
                result.AppendLine($"**{file.Path}** ({file.ChangeType})");
                result.AppendLine("```csharp");
                result.AppendLine(file.Content);
                result.AppendLine("```");
                result.AppendLine();
            }
        }
        else if (status.Status == AgentContracts.Responses.TaskState.Failed && status.Error != null)
        {
            result.AppendLine("**❌ FAILED**");
            result.AppendLine($"- Error: {status.Error.Message}");
            result.AppendLine($"- Type: {status.Error.Type}");
            if (status.Error.CanRetry)
            {
                result.AppendLine("- Can retry: Yes");
            }
        }

        return result.ToString();
    }

    private string HandleCancelTask(Dictionary<string, object> arguments)
    {
        var jobId = GetStringArg(arguments, "jobId");
        var cancelled = _jobManager.CancelJob(jobId);

        return cancelled 
            ? $"✅ Job `{jobId}` has been cancelled" 
            : $"❌ Could not cancel job `{jobId}` - not found or already completed";
    }

    private string HandleListTasks()
    {
        var tasks = _jobManager.GetAllJobs().ToList();

        if (!tasks.Any())
        {
            return "No active tasks";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("**Active Tasks:**");
        result.AppendLine();

        foreach (var task in tasks)
        {
            var statusIcon = task.Status switch
            {
                AgentContracts.Responses.TaskState.Queued => "⏳",
                AgentContracts.Responses.TaskState.Running => "🔄",
                AgentContracts.Responses.TaskState.Complete => "✅",
                AgentContracts.Responses.TaskState.Failed => "❌",
                AgentContracts.Responses.TaskState.Cancelled => "🚫",
                _ => "❓"
            };

            result.AppendLine($"{statusIcon} `{task.JobId}` - {task.Status} ({task.Progress}%) - {task.CurrentPhase}");
        }

        return result.ToString();
    }

    private static string GetStringArg(Dictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? "";
            }
            return value?.ToString() ?? "";
        }
        return "";
    }

    private static bool GetBoolArg(Dictionary<string, object> args, string key, bool defaultValue)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.True;
            }
            if (value is bool b)
            {
                return b;
            }
        }
        return defaultValue;
    }

    private static int GetIntArg(Dictionary<string, object> args, string key, int defaultValue)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.TryGetInt32(out var intValue))
            {
                return intValue;
            }
            if (value is int i)
            {
                return i;
            }
        }
        return defaultValue;
    }
}

