using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for code indexing and querying
/// Tools: index_file, index_directory, query, reindex
/// </summary>
public class IndexingToolHandler : IMcpToolHandler
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly ILogger<IndexingToolHandler> _logger;

    public IndexingToolHandler(
        IIndexingService indexingService,
        IReindexService reindexService,
        ILogger<IndexingToolHandler> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
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
                        context = new { type = "string", description = "Optional context name" },
                        recursive = new { type = "boolean", description = "Whether to index subdirectories", @default = true }
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
            "index_file" => await IndexFileToolAsync(args, cancellationToken),
            "index_directory" => await IndexDirectoryToolAsync(args, cancellationToken),
            "query" => await QueryToolAsync(args, cancellationToken),
            "reindex" => await ReindexToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> IndexFileToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(path))
            return ErrorResult("Path is required");

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
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var recursive = args?.GetValueOrDefault("recursive") as bool? ?? true;

        if (string.IsNullOrWhiteSpace(path))
            return ErrorResult("Path is required");

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
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = args?.GetValueOrDefault("limit") as int? ?? 5;
        var minimumScore = args?.GetValueOrDefault("minimumScore") as float? ?? 0.7f;

        if (string.IsNullOrWhiteSpace(query))
            return ErrorResult("Query is required");

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
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var removeStale = args?.GetValueOrDefault("removeStale") as bool? ?? true;

        if (string.IsNullOrWhiteSpace(path))
            return ErrorResult("Path is required");

        var result = await _reindexService.ReindexAsync(path, context, removeStale, ct);
        
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

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

