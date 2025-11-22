using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Implementation of MCP protocol for code memory
/// </summary>
public class McpService : IMcpService
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly IGraphService _graphService;
    private readonly ISmartSearchService _smartSearchService;
    private readonly ITodoService _todoService;
    private readonly IPlanService _planService;
    private readonly ILogger<McpService> _logger;

    public McpService(
        IIndexingService indexingService,
        IReindexService reindexService,
        IGraphService graphService,
        ISmartSearchService smartSearchService,
        ITodoService todoService,
        IPlanService planService,
        ILogger<McpService> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _graphService = graphService;
        _smartSearchService = smartSearchService;
        _todoService = todoService;
        _planService = planService;
        _logger = logger;
    }

    public Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "index_file",
                Description = "Index a single code file into memory for semantic search and analysis",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file (use /workspace/... for mounted files)" },
                        context = new { type = "string", description = "Optional context name for grouping (e.g., 'ProjectName')" }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "index_directory",
                Description = "Index an entire directory of code files recursively",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to directory (use /workspace/... for mounted files)" },
                        recursive = new { type = "boolean", description = "Whether to index subdirectories", @default = true },
                        context = new { type = "string", description = "Optional context name" }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "query",
                Description = "Search code memory using semantic search. Ask natural language questions about code.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language question (e.g., 'How do we handle errors?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 5 },
                        minimumScore = new { type = "number", description = "Minimum similarity score 0-1", @default = 0.5 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "search",
                Description = "Search code memory using semantic search (alias for query). Ask natural language questions about code.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language question (e.g., 'How do we handle errors?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 5 },
                        minimumScore = new { type = "number", description = "Minimum similarity score 0-1", @default = 0.5 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "reindex",
                Description = "Reindex code to update memory after changes. Detects new, modified, and deleted files.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to directory to reindex" },
                        context = new { type = "string", description = "Context name" },
                        removeStale = new { type = "boolean", description = "Remove deleted files from memory", @default = true }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "impact_analysis",
                Description = "Analyze what code would be impacted if a class changes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "dependency_chain",
                Description = "Get the dependency chain for a class",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" },
                        maxDepth = new { type = "number", description = "Maximum depth", @default = 5 }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "find_circular_dependencies",
                Description = "Find circular dependencies in code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Optional context to search within" }
                    }
                }
            },
            new McpTool
            {
                Name = "smartsearch",
                Description = "Intelligent search that auto-detects whether to use graph-first (for structural queries) or semantic-first (for conceptual queries) strategy. Returns enriched results with relationships and scores.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query (e.g., 'classes that implement IService' or 'how is authentication handled?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results per page", @default = 20 },
                        offset = new { type = "number", description = "Offset for pagination", @default = 0 },
                        includeRelationships = new { type = "boolean", description = "Include relationship data", @default = true },
                        relationshipDepth = new { type = "number", description = "Max relationship depth", @default = 2 }
                    },
                    required = new[] { "query" }
                }
            },
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
            },
            new McpTool
            {
                Name = "create_plan",
                Description = "Create a development plan with tasks and dependencies",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        name = new { type = "string", description = "Plan name" },
                        description = new { type = "string", description = "Plan description" },
                        tasks = new
                        {
                            type = "array",
                            description = "Array of tasks",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new { type = "string" },
                                    description = new { type = "string" },
                                    orderIndex = new { type = "number" }
                                }
                            }
                        }
                    },
                    required = new[] { "context", "name", "tasks" }
                }
            },
            new McpTool
            {
                Name = "get_plan_status",
                Description = "Get the status and progress of a development plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "update_task_status",
                Description = "Update the status of a task in a plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" },
                        taskId = new { type = "string", description = "Task ID" },
                        status = new { type = "string", description = "New status: Pending, InProgress, Blocked, Completed, Cancelled" }
                    },
                    required = new[] { "planId", "taskId", "status" }
                }
            },
            new McpTool
            {
                Name = "complete_plan",
                Description = "Mark a development plan as completed",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "search_plans",
                Description = "Search and filter development plans",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Filter by context" },
                        status = new { type = "string", description = "Filter by status: Draft, Active, Completed, Cancelled, OnHold" }
                    }
                }
            }
        };

        return Task.FromResult(tools);
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling tool: {Tool} with args: {Args}", 
                toolCall.Name, JsonSerializer.Serialize(toolCall.Arguments));

            var result = toolCall.Name switch
            {
                "index_file" => await IndexFileToolAsync(toolCall.Arguments, cancellationToken),
                "index_directory" => await IndexDirectoryToolAsync(toolCall.Arguments, cancellationToken),
                "query" => await QueryToolAsync(toolCall.Arguments, cancellationToken),
                "search" => await QueryToolAsync(toolCall.Arguments, cancellationToken), // Alias for query
                "reindex" => await ReindexToolAsync(toolCall.Arguments, cancellationToken),
                "smartsearch" => await SmartSearchToolAsync(toolCall.Arguments, cancellationToken),
                "impact_analysis" => await ImpactAnalysisToolAsync(toolCall.Arguments, cancellationToken),
                "dependency_chain" => await DependencyChainToolAsync(toolCall.Arguments, cancellationToken),
                "find_circular_dependencies" => await CircularDependenciesToolAsync(toolCall.Arguments, cancellationToken),
                "add_todo" => await AddTodoToolAsync(toolCall.Arguments, cancellationToken),
                "search_todos" => await SearchTodosToolAsync(toolCall.Arguments, cancellationToken),
                "update_todo_status" => await UpdateTodoStatusToolAsync(toolCall.Arguments, cancellationToken),
                "create_plan" => await CreatePlanToolAsync(toolCall.Arguments, cancellationToken),
                "get_plan_status" => await GetPlanStatusToolAsync(toolCall.Arguments, cancellationToken),
                "update_task_status" => await UpdateTaskStatusToolAsync(toolCall.Arguments, cancellationToken),
                "complete_plan" => await CompletePlanToolAsync(toolCall.Arguments, cancellationToken),
                "search_plans" => await SearchPlansToolAsync(toolCall.Arguments, cancellationToken),
                _ => new McpToolResult
                {
                    IsError = true,
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = $"Unknown tool: {toolCall.Name}" }
                    }
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool: {Tool}", toolCall.Name);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error: {ex.Message}" }
                }
            };
        }
    }

    public async Task<McpResponse?> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Handling MCP request: {Method}", request.Method);

            // Notifications don't get responses (MCP protocol spec)
            if (request.Method.StartsWith("notifications/"))
            {
                _logger.LogInformation("Notification received: {Method} (no response will be sent)", request.Method);
                return null;
            }

            var result = request.Method switch
            {
                "tools/list" => new McpResponse
                {
                    Id = request.Id,
                    Result = new { tools = await GetToolsAsync(cancellationToken) }
                },
                "tools/call" => await HandleToolCallAsync(request, cancellationToken),
                "initialize" => new McpResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "memory-code-agent",
                            version = "1.0.0"
                        }
                    }
                },
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    }
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            };
        }
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var paramsJson = JsonSerializer.Serialize(request.Params);
        var toolCall = JsonSerializer.Deserialize<McpToolCall>(paramsJson);
        
        if (toolCall == null)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32602, Message = "Invalid params" }
            };
        }

        var result = await CallToolAsync(toolCall, cancellationToken);
        
        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    // Tool implementations
    private async Task<McpToolResult> IndexFileToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();

        if (string.IsNullOrWhiteSpace(path))
        {
            return ErrorResult("Path is required");
        }

        var result = await _indexingService.IndexFileAsync(path, context, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> IndexDirectoryToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var recursive = args?.GetValueOrDefault("recursive") as bool? ?? true;

        if (string.IsNullOrWhiteSpace(path))
        {
            return ErrorResult("Path is required");
        }

        var result = await _indexingService.IndexDirectoryAsync(path, recursive, context, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> QueryToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var limit = args?.GetValueOrDefault("limit") as int? ?? 5;
        var minimumScore = args?.GetValueOrDefault("minimumScore") as float? ?? 0.7f;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("Query is required");
        }

        var result = await _indexingService.QueryAsync(query, context, limit, minimumScore, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ReindexToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var removeStale = args?.GetValueOrDefault("removeStale") as bool? ?? true;

        var result = await _reindexService.ReindexAsync(context, path, removeStale, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> SmartSearchToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var limit = args?.GetValueOrDefault("limit") as int? ?? 20;
        var offset = args?.GetValueOrDefault("offset") as int? ?? 0;
        var includeRelationships = args?.GetValueOrDefault("includeRelationships") as bool? ?? true;
        var relationshipDepth = args?.GetValueOrDefault("relationshipDepth") as int? ?? 2;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("Query is required");
        }

        var request = new SmartSearchRequest
        {
            Query = query,
            Context = context,
            Limit = limit,
            Offset = offset,
            IncludeRelationships = includeRelationships,
            RelationshipDepth = relationshipDepth
        };

        var result = await _smartSearchService.SearchAsync(request, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ImpactAnalysisToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();

        if (string.IsNullOrWhiteSpace(className))
        {
            return ErrorResult("className is required");
        }

        var impacted = await _graphService.GetImpactAnalysisAsync(className, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Classes impacted by changes to {className}:\n" + 
                           string.Join("\n", impacted.Select(c => $"- {c}"))
                }
            }
        };
    }

    private async Task<McpToolResult> DependencyChainToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();
        var maxDepth = args?.GetValueOrDefault("maxDepth") as int? ?? 5;

        if (string.IsNullOrWhiteSpace(className))
        {
            return ErrorResult("className is required");
        }

        var dependencies = await _graphService.GetDependencyChainAsync(className, maxDepth, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Dependencies for {className}:\n" + 
                           string.Join("\n", dependencies.Select(d => $"- {d}"))
                }
            }
        };
    }

    private async Task<McpToolResult> CircularDependenciesToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();

        var cycles = await _graphService.FindCircularDependenciesAsync(context, ct);
        
        if (!cycles.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "No circular dependencies found!" }
                }
            };
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Found {cycles.Count} circular dependency cycles:\n" +
                           string.Join("\n\n", cycles.Select((cycle, i) => 
                               $"Cycle {i + 1}: {string.Join(" → ", cycle)}"))
                }
            }
        };
    }

    private async Task<McpToolResult> AddTodoToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var context = args.GetProperty("context").GetString() ?? "default";
        var title = args.GetProperty("title").GetString() ?? "";
        var description = args.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        var priority = args.TryGetProperty("priority", out var prio) ? Enum.Parse<TodoPriority>(prio.GetString() ?? "Medium") : TodoPriority.Medium;
        var filePath = args.TryGetProperty("filePath", out var file) ? file.GetString() ?? "" : "";
        var lineNumber = args.TryGetProperty("lineNumber", out var line) ? line.GetInt32() : 0;
        var assignedTo = args.TryGetProperty("assignedTo", out var assigned) ? assigned.GetString() ?? "" : "";

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

    private async Task<McpToolResult> SearchTodosToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var context = args.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;
        var statusStr = args.TryGetProperty("status", out var stat) ? stat.GetString() : null;
        var priorityStr = args.TryGetProperty("priority", out var prio) ? prio.GetString() : null;
        var assignedTo = args.TryGetProperty("assignedTo", out var assigned) ? assigned.GetString() : null;

        TodoStatus? status = statusStr != null ? Enum.Parse<TodoStatus>(statusStr) : null;
        
        var todos = await _todoService.GetTodosAsync(context, status, cancellationToken);

        // Filter by priority and assignedTo
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

    private async Task<McpToolResult> UpdateTodoStatusToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var todoId = args.GetProperty("todoId").GetString() ?? "";
        var statusStr = args.GetProperty("status").GetString() ?? "";
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

    private async Task<McpToolResult> CreatePlanToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var context = args.GetProperty("context").GetString() ?? "default";
        var name = args.GetProperty("name").GetString() ?? "";
        var description = args.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        
        var tasks = new List<PlanTaskRequest>();
        if (args.TryGetProperty("tasks", out var tasksArr))
        {
            int index = 0;
            foreach (var taskElem in tasksArr.EnumerateArray())
            {
                tasks.Add(new PlanTaskRequest
                {
                    Title = taskElem.GetProperty("title").GetString() ?? "",
                    Description = taskElem.TryGetProperty("description", out var taskDesc) ? taskDesc.GetString() ?? "" : "",
                    OrderIndex = taskElem.TryGetProperty("orderIndex", out var order) ? order.GetInt32() : index++,
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

        var plan = await _planService.AddPlanAsync(request, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Development Plan created!\n\n" +
                           $"ID: {plan.Id}\n" +
                           $"Name: {plan.Name}\n" +
                           $"Status: {plan.Status}\n" +
                           $"Tasks: {plan.Tasks.Count}\n" +
                           $"Created: {plan.CreatedAt:yyyy-MM-dd HH:mm}"
                }
            }
        };
    }

    private async Task<McpToolResult> GetPlanStatusToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var planId = args.GetProperty("planId").GetString() ?? "";
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);

        if (plan == null)
        {
            return ErrorResult($"Plan not found: {planId}");
        }

        var total = plan.Tasks.Count;
        var completed = plan.Tasks.Count(t => t.Status == TaskStatus.Completed);
        var inProgress = plan.Tasks.Count(t => t.Status == TaskStatus.InProgress);
        var pending = plan.Tasks.Count(t => t.Status == TaskStatus.Pending);
        var progress = total > 0 ? (double)completed / total * 100 : 0;

        var text = $"📋 {plan.Name}\n\n" +
                   $"Status: {plan.Status}\n" +
                   $"Progress: {progress:F1}% ({completed}/{total} tasks completed)\n\n" +
                   $"Tasks:\n" +
                   string.Join("\n", plan.Tasks.OrderBy(t => t.OrderIndex).Select(t =>
                       $"  {(t.Status == TaskStatus.Completed ? "✅" : t.Status == TaskStatus.InProgress ? "🔄" : t.Status == TaskStatus.Blocked ? "🚫" : "⏳")} {t.Title} ({t.Status})"));

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> UpdateTaskStatusToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var planId = args.GetProperty("planId").GetString() ?? "";
        var taskId = args.GetProperty("taskId").GetString() ?? "";
        var statusStr = args.GetProperty("status").GetString() ?? "";
        var status = Enum.Parse<TaskStatus>(statusStr);

        var plan = await _planService.UpdateTaskStatusAsync(planId, taskId, status, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Task status updated!\n\n" +
                           $"Plan: {plan.Name}\n" +
                           $"Progress: {(double)plan.Tasks.Count(t => t.Status == TaskStatus.Completed) / plan.Tasks.Count * 100:F1}%"
                }
            }
        };
    }

    private async Task<McpToolResult> CompletePlanToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var planId = args.GetProperty("planId").GetString() ?? "";
        var plan = await _planService.CompletePlanAsync(planId, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Plan completed!\n\n" +
                           $"Name: {plan.Name}\n" +
                           $"Completed: {plan.CompletedAt:yyyy-MM-dd HH:mm}\n" +
                           $"Total tasks: {plan.Tasks.Count}"
                }
            }
        };
    }

    private async Task<McpToolResult> SearchPlansToolAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var context = args.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;
        var statusStr = args.TryGetProperty("status", out var stat) ? stat.GetString() : null;
        PlanStatus? status = statusStr != null ? Enum.Parse<PlanStatus>(statusStr) : null;

        var plans = await _planService.GetPlansAsync(context, status, cancellationToken);

        var text = plans.Any()
            ? $"Found {plans.Count} plan(s):\n\n" +
              string.Join("\n\n", plans.Select(p =>
              {
                  var total = p.Tasks.Count;
                  var completed = p.Tasks.Count(t => t.Status == TaskStatus.Completed);
                  var progress = total > 0 ? (double)completed / total * 100 : 0;
                  return $"📋 {p.Name}\n" +
                         $"   ID: {p.Id}\n" +
                         $"   Status: {p.Status}\n" +
                         $"   Progress: {progress:F1}% ({completed}/{total})\n" +
                         $"   Created: {p.CreatedAt:yyyy-MM-dd}";
              }))
            : "No plans found.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private static McpToolResult ErrorResult(string message)
    {
        return new McpToolResult
        {
            IsError = true,
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"Error: {message}" }
            }
        };
    }
}


