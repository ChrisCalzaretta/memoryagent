using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for planning and TODO operations.
/// Tools: create_plan, manage_plan, add_todo, manage_todos
/// </summary>
public class PlanningToolHandler : IMcpToolHandler
{
    private readonly IPlanService _planService;
    private readonly ITodoService _todoService;
    private readonly IRecommendationService _recommendationService;
    private readonly ITaskValidationService _taskValidationService;
    private readonly IIntentClassificationService _intentClassifier;
    private readonly ILogger<PlanningToolHandler> _logger;

    public PlanningToolHandler(
        IPlanService planService,
        ITodoService todoService,
        IRecommendationService recommendationService,
        ITaskValidationService taskValidationService,
        IIntentClassificationService intentClassifier,
        ILogger<PlanningToolHandler> logger)
    {
        _planService = planService;
        _todoService = todoService;
        _recommendationService = recommendationService;
        _taskValidationService = taskValidationService;
        _intentClassifier = intentClassifier;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "manage_plan",
                Description = "Manage development plans. Actions: 'create', 'get_status', 'update_task', 'complete', 'search', 'validate_task'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", description = "Action to perform", @enum = new[] { "create", "get_status", "update_task", "complete", "search", "validate_task" } },
                        // For create
                        context = new { type = "string", description = "Project context (for create, search)" },
                        name = new { type = "string", description = "Plan name (for create)" },
                        description = new { type = "string", description = "Plan description (for create)" },
                        tasks = new { type = "array", items = new { type = "object" }, description = "Array of tasks [{title, description, orderIndex}] (for create)" },
                        includeRecommendations = new { type = "boolean", description = "Auto-generate tasks from recommendations (for create)", @default = false },
                        maxRecommendations = new { type = "number", description = "Max recommended tasks (for create)", @default = 10 },
                        // For other actions
                        planId = new { type = "string", description = "Plan ID (for get_status, update_task, complete, validate_task)" },
                        taskId = new { type = "string", description = "Task ID (for update_task, validate_task)" },
                        status = new { type = "string", description = "New status (for update_task): Pending, InProgress, Blocked, Completed, Cancelled" },
                        planStatus = new { type = "string", description = "Filter by plan status (for search): Draft, Active, Completed, Cancelled, OnHold" },
                        autoFix = new { type = "boolean", description = "Auto-fix validation failures (for validate_task)", @default = false }
                    },
                    required = new[] { "action" }
                }
            },
            new McpTool
            {
                Name = "manage_todos",
                Description = "Manage TODO items. Actions: 'add', 'search', 'update_status'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", description = "Action to perform", @enum = new[] { "add", "search", "update_status" } },
                        // For add
                        context = new { type = "string", description = "Project context (for add, search)" },
                        title = new { type = "string", description = "TODO title (for add)" },
                        description = new { type = "string", description = "Detailed description (for add)" },
                        priority = new { type = "string", description = "Priority (for add, search): Low, Medium, High, Critical", @default = "Medium" },
                        filePath = new { type = "string", description = "Related file path (for add)" },
                        lineNumber = new { type = "number", description = "Line number (for add)" },
                        assignedTo = new { type = "string", description = "Assignee (for add, search)" },
                        // For update_status
                        todoId = new { type = "string", description = "TODO ID (for update_status)" },
                        status = new { type = "string", description = "New status (for update_status): Pending, InProgress, Completed, Cancelled" },
                        // For search
                        todoStatus = new { type = "string", description = "Filter by status (for search)" }
                    },
                    required = new[] { "action" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "manage_plan" => await ManagePlanAsync(args, cancellationToken),
            "manage_todos" => await ManageTodosAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Plan Management

    private async Task<McpToolResult> CreatePlanAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var name = args?.GetValueOrDefault("name")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        var includeRecommendations = SafeParseBool(args?.GetValueOrDefault("includeRecommendations"), false);
        var maxRecommendations = SafeParseInt(args?.GetValueOrDefault("maxRecommendations"), 10);
        
        var tasks = new List<PlanTaskRequest>();
        
        // Parse manual tasks
        if (args?.TryGetValue("tasks", out var tasksArr) == true)
        {
            tasks = ParseTasks(tasksArr);
        }

        // Auto-generate recommended tasks
        if (includeRecommendations)
        {
            _logger.LogInformation("🎯 Analyzing {Context} for architecture recommendations...", context);
            
            var userRequest = $"{name}. {description}";
            var intent = await _intentClassifier.ClassifyIntentAsync(userRequest, context, ct);
            var suggestedCategories = await _intentClassifier.SuggestPatternCategoriesAsync(intent, ct);

            var recRequest = new RecommendationRequest
            {
                Context = context,
                Categories = suggestedCategories.Any() ? suggestedCategories : null,
                IncludeLowPriority = false,
                MaxRecommendations = maxRecommendations
            };

            var recommendations = await _recommendationService.AnalyzeAndRecommendAsync(recRequest, ct);

            int taskIndex = tasks.Count;
            foreach (var rec in recommendations.Recommendations
                .Where(r => r.Priority == "HIGH" || r.Priority == "CRITICAL")
                .Take(maxRecommendations))
            {
                tasks.Add(new PlanTaskRequest
                {
                    Title = $"[{rec.Category}] {rec.Issue}",
                    Description = $"{rec.Recommendation}\n\n🎯 Impact: {rec.Impact}",
                    OrderIndex = taskIndex++,
                    Dependencies = new List<string>()
                });
            }
        }

        var request = new AddPlanRequest
        {
            Context = context,
            Name = name,
            Description = description,
            Tasks = tasks
        };

        var plan = await _planService.AddPlanAsync(request, ct);

        var output = $"✅ Development Plan Created!\n\n";
        output += $"ID: {plan.Id}\n";
        output += $"Name: {plan.Name}\n";
        output += $"Tasks: {plan.Tasks.Count}\n";
        output += $"Context: {context}\n";
        
        if (includeRecommendations)
        {
            var recCount = tasks.Count(t => t.Title.StartsWith("["));
            output += $"\n🎯 {recCount} tasks auto-generated from architecture recommendations";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> ManagePlanAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var action = args?.GetValueOrDefault("action")?.ToString()?.ToLowerInvariant();
        
        return action switch
        {
            "create" => await CreatePlanAsync(args, ct),
            "get_status" => await GetPlanStatusAsync(args, ct),
            "update_task" => await UpdateTaskStatusAsync(args, ct),
            "complete" => await CompletePlanAsync(args, ct),
            "search" => await SearchPlansAsync(args, ct),
            "validate_task" => await ValidateTaskAsync(args, ct),
            _ => ErrorResult($"Unknown plan action: {action}. Valid: create, get_status, update_task, complete, search, validate_task")
        };
    }

    private async Task<McpToolResult> GetPlanStatusAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString();
        if (string.IsNullOrWhiteSpace(planId))
            return ErrorResult("planId is required for get_status");

        var plan = await _planService.GetPlanAsync(planId, ct);
        if (plan == null)
            return ErrorResult($"Plan not found: {planId}");

        var total = plan.Tasks.Count;
        var completed = plan.Tasks.Count(t => t.Status == Models.TaskStatus.Completed);
        var inProgress = plan.Tasks.Count(t => t.Status == Models.TaskStatus.InProgress);
        var progress = total > 0 ? (double)completed / total * 100 : 0;

        var output = $"📋 {plan.Name}\n\n";
        output += $"Status: {plan.Status}\n";
        output += $"Progress: {progress:F0}% ({completed}/{total} completed)\n";
        output += $"In Progress: {inProgress}\n\n";
        output += "Tasks:\n";
        
        foreach (var t in plan.Tasks.OrderBy(t => t.OrderIndex))
        {
            var icon = t.Status switch
            {
                Models.TaskStatus.Completed => "✅",
                Models.TaskStatus.InProgress => "🔄",
                Models.TaskStatus.Blocked => "🚫",
                _ => "⏳"
            };
            output += $"  {icon} {t.Title} ({t.Status})\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> UpdateTaskStatusAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString();
        var taskId = args?.GetValueOrDefault("taskId")?.ToString();
        var statusStr = args?.GetValueOrDefault("status")?.ToString();

        if (string.IsNullOrWhiteSpace(planId) || string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(statusStr))
            return ErrorResult("planId, taskId, and status are required for update_task");

        if (!Enum.TryParse<Models.TaskStatus>(statusStr, true, out var status))
            return ErrorResult($"Invalid status: {statusStr}");

        var plan = await _planService.UpdateTaskStatusAsync(planId, taskId, status, ct);
        var progress = (double)plan.Tasks.Count(t => t.Status == Models.TaskStatus.Completed) / plan.Tasks.Count * 100;

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"✅ Task updated!\n\nPlan: {plan.Name}\nProgress: {progress:F0}%" }
            }
        };
    }

    private async Task<McpToolResult> CompletePlanAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString();
        if (string.IsNullOrWhiteSpace(planId))
            return ErrorResult("planId is required for complete");

        var plan = await _planService.CompletePlanAsync(planId, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"✅ Plan Completed!\n\nName: {plan.Name}\nCompleted: {plan.CompletedAt:yyyy-MM-dd HH:mm}\nTotal Tasks: {plan.Tasks.Count}" }
            }
        };
    }

    private async Task<McpToolResult> SearchPlansAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var statusStr = args?.GetValueOrDefault("planStatus")?.ToString();
        PlanStatus? status = statusStr != null && Enum.TryParse<PlanStatus>(statusStr, true, out var s) ? s : null;

        var plans = await _planService.GetPlansAsync(context, status, ct);

        if (!plans.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "No plans found." }
                }
            };
        }

        var output = $"📋 Found {plans.Count} plan(s):\n\n";
        foreach (var p in plans)
        {
            var total = p.Tasks.Count;
            var completed = p.Tasks.Count(t => t.Status == Models.TaskStatus.Completed);
            var progress = total > 0 ? (double)completed / total * 100 : 0;
            
            output += $"• {p.Name}\n";
            output += $"  ID: {p.Id}\n";
            output += $"  Status: {p.Status} | Progress: {progress:F0}%\n";
            output += $"  Created: {p.CreatedAt:yyyy-MM-dd}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> ValidateTaskAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString();
        var taskId = args?.GetValueOrDefault("taskId")?.ToString();
        var autoFix = SafeParseBool(args?.GetValueOrDefault("autoFix"), false);

        if (string.IsNullOrWhiteSpace(planId) || string.IsNullOrWhiteSpace(taskId))
            return ErrorResult("planId and taskId are required for validate_task");

        var plan = await _planService.GetPlanAsync(planId, ct);
        if (plan == null)
            return ErrorResult($"Plan not found: {planId}");

        var task = plan.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
            return ErrorResult($"Task not found: {taskId}");

        var result = await _taskValidationService.ValidateTaskAsync(task, plan.Context, ct);

        if (result.IsValid)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"✅ Task '{task.Title}' passed all validation!\n\nReady to mark as completed." }
                }
            };
        }

        var output = $"❌ Task '{task.Title}' failed validation:\n\n";
        foreach (var failure in result.Failures)
        {
            output += $"• {failure.RuleType}: {failure.Message}\n";
            if (failure.CanAutoFix)
                output += $"  💡 Auto-fix available\n";
        }

        if (autoFix)
        {
            output += "\n🔧 Attempting auto-fix...\n";
            var wasFixed = await _taskValidationService.AutoFixValidationFailuresAsync(task, result, plan.Context, ct);
            output += wasFixed ? "✅ Auto-fix completed! Re-validate to confirm.\n" : "❌ Auto-fix failed.\n";
        }

        return new McpToolResult
        {
            IsError = !autoFix,
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region TODO Management

    private async Task<McpToolResult> AddTodoAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var title = args?.GetValueOrDefault("title")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        var priorityStr = args?.GetValueOrDefault("priority")?.ToString() ?? "Medium";
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var lineNumber = SafeParseInt(args?.GetValueOrDefault("lineNumber"), 0);
        var assignedTo = args?.GetValueOrDefault("assignedTo")?.ToString();

        if (!Enum.TryParse<TodoPriority>(priorityStr, true, out var priority))
            priority = TodoPriority.Medium;

        var request = new AddTodoRequest
        {
            Context = context,
            Title = title,
            Description = description,
            Priority = priority,
            FilePath = filePath ?? "",
            LineNumber = lineNumber,
            AssignedTo = assignedTo ?? ""
        };

        var todo = await _todoService.AddTodoAsync(request, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"✅ TODO Added!\n\nID: {todo.Id}\nTitle: {todo.Title}\nPriority: {todo.Priority}\nStatus: {todo.Status}" }
            }
        };
    }

    private async Task<McpToolResult> ManageTodosAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var action = args?.GetValueOrDefault("action")?.ToString()?.ToLowerInvariant();

        return action switch
        {
            "add" => await AddTodoAsync(args, ct),
            "search" => await SearchTodosAsync(args, ct),
            "update_status" => await UpdateTodoStatusAsync(args, ct),
            _ => ErrorResult($"Unknown todo action: {action}. Valid: add, search, update_status")
        };
    }

    private async Task<McpToolResult> SearchTodosAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var statusStr = args?.GetValueOrDefault("todoStatus")?.ToString();
        var priorityStr = args?.GetValueOrDefault("priority")?.ToString();
        var assignedTo = args?.GetValueOrDefault("assignedTo")?.ToString();

        TodoStatus? status = statusStr != null && Enum.TryParse<TodoStatus>(statusStr, true, out var s) ? s : null;

        var todos = await _todoService.GetTodosAsync(context, status, ct);

        if (priorityStr != null && Enum.TryParse<TodoPriority>(priorityStr, true, out var priority))
            todos = todos.Where(t => t.Priority == priority).ToList();
        if (!string.IsNullOrEmpty(assignedTo))
            todos = todos.Where(t => t.AssignedTo.Contains(assignedTo, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!todos.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "No TODOs found." }
                }
            };
        }

        var output = $"📌 Found {todos.Count} TODO(s):\n\n";
        foreach (var t in todos.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt))
        {
            var priorityIcon = t.Priority switch
            {
                TodoPriority.Critical => "🔴",
                TodoPriority.High => "🟠",
                TodoPriority.Medium => "🟡",
                _ => "🟢"
            };
            output += $"{priorityIcon} {t.Title}\n";
            output += $"   ID: {t.Id} | Status: {t.Status}\n";
            if (!string.IsNullOrEmpty(t.AssignedTo))
                output += $"   Assigned: {t.AssignedTo}\n";
            output += $"   Created: {t.CreatedAt:yyyy-MM-dd}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> UpdateTodoStatusAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var todoId = args?.GetValueOrDefault("todoId")?.ToString();
        var statusStr = args?.GetValueOrDefault("status")?.ToString();

        if (string.IsNullOrWhiteSpace(todoId) || string.IsNullOrWhiteSpace(statusStr))
            return ErrorResult("todoId and status are required for update_status");

        if (!Enum.TryParse<TodoStatus>(statusStr, true, out var status))
            return ErrorResult($"Invalid status: {statusStr}");

        var todo = await _todoService.UpdateTodoAsync(todoId, status, ct);

        var output = $"✅ TODO Updated!\n\nTitle: {todo.Title}\nStatus: {todo.Status}";
        if (todo.CompletedAt.HasValue)
            output += $"\nCompleted: {todo.CompletedAt:yyyy-MM-dd HH:mm}";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Helpers

    private static List<PlanTaskRequest> ParseTasks(object? value)
    {
        var tasks = new List<PlanTaskRequest>();
        if (value == null) return tasks;

        IEnumerable<object>? items = value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Array => je.EnumerateArray().Cast<object>(),
            IEnumerable<object> enumerable => enumerable,
            _ => null
        };

        if (items == null) return tasks;

        int index = 0;
        foreach (var item in items)
        {
            string? title = null;
            string? desc = null;
            int order = index;

            if (item is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                if (je.TryGetProperty("title", out var t)) title = t.GetString();
                if (je.TryGetProperty("description", out var d)) desc = d.GetString();
                if (je.TryGetProperty("orderIndex", out var o) && o.TryGetInt32(out var oi)) order = oi;
            }
            else if (item is Dictionary<string, object> dict)
            {
                title = dict.GetValueOrDefault("title")?.ToString();
                desc = dict.GetValueOrDefault("description")?.ToString();
                if (dict.TryGetValue("orderIndex", out var o)) order = SafeParseInt(o, index);
            }

            if (!string.IsNullOrEmpty(title))
            {
                tasks.Add(new PlanTaskRequest
                {
                    Title = title,
                    Description = desc ?? "",
                    OrderIndex = order,
                    Dependencies = new List<string>()
                });
            }
            index++;
        }

        return tasks;
    }

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.TryGetInt32(out var i) => i,
            _ => defaultValue
        };

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => defaultValue
        };

    private static McpToolResult ErrorResult(string error) => new()
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };

    #endregion
}

