using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for TODO management
/// Tools: add_todo, search_todos, update_todo_status
/// </summary>
public class TodoToolHandler : IMcpToolHandler
{
    private readonly ITodoService _todoService;
    private readonly ILogger<TodoToolHandler> _logger;

    public TodoToolHandler(
        ITodoService todoService,
        ILogger<TodoToolHandler> logger)
    {
        _todoService = todoService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "add_todo",
                Description = "Add a TODO item to track technical debt, bugs, or improvements",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        title = new { type = "string", description = "TODO title" },
                        description = new { type = "string", description = "Detailed description" },
                        priority = new { type = "string", description = "Priority: Low, Medium, High, Critical", @default = "Medium" },
                        filePath = new { type = "string", description = "Optional file path" },
                        lineNumber = new { type = "number", description = "Optional line number" },
                        assignedTo = new { type = "string", description = "Optional assignee email" }
                    },
                    required = new[] { "context", "title" }
                }
            },
            new McpTool
            {
                Name = "search_todos",
                Description = "Search and filter TODO items",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Filter by context" },
                        status = new { type = "string", description = "Filter by status: Pending, InProgress, Completed, Cancelled" },
                        priority = new { type = "string", description = "Filter by priority: Low, Medium, High, Critical" },
                        assignedTo = new { type = "string", description = "Filter by assignee" }
                    }
                }
            },
            new McpTool
            {
                Name = "update_todo_status",
                Description = "Update the status of a TODO item",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        todoId = new { type = "string", description = "TODO ID" },
                        status = new { type = "string", description = "New status: Pending, InProgress, Completed, Cancelled" }
                    },
                    required = new[] { "todoId", "status" }
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
            "add_todo" => await AddTodoToolAsync(args, cancellationToken),
            "search_todos" => await SearchTodosToolAsync(args, cancellationToken),
            "update_todo_status" => await UpdateTodoStatusToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> AddTodoToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var title = args?.GetValueOrDefault("title")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        var priority = Enum.TryParse<TodoPriority>(args?.GetValueOrDefault("priority")?.ToString(), out var parsedPriority) ? parsedPriority : TodoPriority.Medium;
        var filePath = args?.GetValueOrDefault("filePath")?.ToString() ?? "";
        var lineNumber = (args?.GetValueOrDefault("lineNumber") as int?) ?? 0;
        var assignedTo = args?.GetValueOrDefault("assignedTo")?.ToString() ?? "";

        var request = new AddTodoRequest
        {
            Context = context,
            Title = title,
            Description = description,
            Priority = priority,
            FilePath = filePath,
            LineNumber = lineNumber,
            AssignedTo = assignedTo
        };

        var todo = await _todoService.AddTodoAsync(request, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ TODO added successfully!\n\n" +
                           $"ID: {todo.Id}\n" +
                           $"Title: {todo.Title}\n" +
                           $"Priority: {todo.Priority}\n" +
                           $"Status: {todo.Status}\n" +
                           $"Created: {todo.CreatedAt:yyyy-MM-dd HH:mm}"
                }
            }
        };
    }

    private async Task<McpToolResult> SearchTodosToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.TryGetValue("context", out var ctx) == true ? ctx?.ToString()?.ToLowerInvariant() : null;
        var statusStr = args?.TryGetValue("status", out var stat) == true ? stat?.ToString() : null;
        var priorityStr = args?.TryGetValue("priority", out var prio) == true ? prio?.ToString() : null;
        var assignedTo = args?.TryGetValue("assignedTo", out var assigned) == true ? assigned?.ToString() : null;

        TodoStatus? status = statusStr != null ? Enum.Parse<TodoStatus>(statusStr) : null;
        
        var todos = await _todoService.GetTodosAsync(context, status, cancellationToken);

        if (priorityStr != null)
        {
            var priority = Enum.Parse<TodoPriority>(priorityStr);
            todos = todos.Where(t => t.Priority == priority).ToList();
        }
        if (assignedTo != null)
        {
            todos = todos.Where(t => t.AssignedTo.Contains(assignedTo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var text = todos.Any()
            ? $"Found {todos.Count} TODO(s):\n\n" +
              string.Join("\n\n", todos.Select(t =>
                  $"📌 {t.Title}\n" +
                  $"   ID: {t.Id}\n" +
                  $"   Priority: {t.Priority}\n" +
                  $"   Status: {t.Status}\n" +
                  $"   {(string.IsNullOrEmpty(t.AssignedTo) ? "" : $"Assigned: {t.AssignedTo}\n   ")}" +
                  $"   Created: {t.CreatedAt:yyyy-MM-dd}"))
            : "No TODOs found.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> UpdateTodoStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var todoId = args?.GetValueOrDefault("todoId")?.ToString() ?? "";
        var statusStr = args?.GetValueOrDefault("status")?.ToString() ?? "";
        var status = Enum.Parse<TodoStatus>(statusStr);

        var todo = await _todoService.UpdateTodoAsync(todoId, status, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ TODO status updated!\n\n" +
                           $"Title: {todo.Title}\n" +
                           $"Status: {todo.Status}\n" +
                           $"{(todo.CompletedAt.HasValue ? $"Completed: {todo.CompletedAt:yyyy-MM-dd HH:mm}" : "")}"
                }
            }
        };
    }

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

