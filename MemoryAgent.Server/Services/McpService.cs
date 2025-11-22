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
    private readonly ILogger<McpService> _logger;

    public McpService(
        IIndexingService indexingService,
        IReindexService reindexService,
        IGraphService graphService,
        ILogger<McpService> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _graphService = graphService;
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
                "impact_analysis" => await ImpactAnalysisToolAsync(toolCall.Arguments, cancellationToken),
                "dependency_chain" => await DependencyChainToolAsync(toolCall.Arguments, cancellationToken),
                "find_circular_dependencies" => await CircularDependenciesToolAsync(toolCall.Arguments, cancellationToken),
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


