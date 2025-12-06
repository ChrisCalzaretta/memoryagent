using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for all indexing operations.
/// Tool: index
/// Consolidates: index_file, index_directory, reindex
/// </summary>
public class IndexToolHandler : IMcpToolHandler
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly ILogger<IndexToolHandler> _logger;

    public IndexToolHandler(
        IIndexingService indexingService,
        IReindexService reindexService,
        ILogger<IndexToolHandler> logger)
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
                Name = "index",
                Description = "Index code into memory. Use scope='file' for single file, scope='directory' for folder, scope='reindex' to update after changes.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to file or directory to index" },
                        context = new { type = "string", description = "Project context name" },
                        scope = new { type = "string", description = "Index scope: 'file' (single file), 'directory' (folder), 'reindex' (update changes)", @default = "file", @enum = new[] { "file", "directory", "reindex" } },
                        recursive = new { type = "boolean", description = "For directory scope: index subdirectories (default: true)", @default = true },
                        removeStale = new { type = "boolean", description = "For reindex scope: remove deleted files from memory (default: true)", @default = true }
                    },
                    required = new[] { "path", "context" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        if (toolName == "index")
            return await IndexToolAsync(args, cancellationToken);

        return ErrorResult($"Unknown tool: {toolName}");
    }

    private async Task<McpToolResult> IndexToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var scope = args?.GetValueOrDefault("scope")?.ToString()?.ToLowerInvariant() ?? "file";

        if (string.IsNullOrWhiteSpace(path))
            return ErrorResult("path is required");
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        IndexResult result;
        string operation;

        switch (scope)
        {
            case "directory":
                var recursive = SafeParseBool(args?.GetValueOrDefault("recursive"), true);
                operation = recursive ? "Directory (recursive)" : "Directory (non-recursive)";
                result = await _indexingService.IndexDirectoryAsync(path, recursive, context, ct);
                break;

            case "reindex":
                var removeStale = SafeParseBool(args?.GetValueOrDefault("removeStale"), true);
                operation = removeStale ? "Reindex (with cleanup)" : "Reindex (preserve stale)";
                var reindexResult = await _reindexService.ReindexAsync(context, path, removeStale, ct);
                // Map ReindexResult to IndexResult for unified output
                result = new IndexResult 
                { 
                    Success = reindexResult.Success, 
                    FilesIndexed = reindexResult.FilesAdded + reindexResult.FilesUpdated,
                    Errors = reindexResult.Errors
                };
                break;

            case "file":
            default:
                operation = "Single file";
                result = await _indexingService.IndexFileAsync(path, context, ct);
                break;
        }

        // Format output
        var statusIcon = result.Success ? "✅" : "❌";
        var output = $"{statusIcon} Index {operation}\n\n";
        output += $"Path: {path}\n";
        output += $"Context: {context}\n";
        output += $"Success: {result.Success}\n\n";

        if (result.FilesIndexed > 0 || result.ClassesFound > 0 || result.MethodsFound > 0)
        {
            output += "📊 Statistics:\n";
            output += $"  • Files indexed: {result.FilesIndexed}\n";
            output += $"  • Classes found: {result.ClassesFound}\n";
            output += $"  • Methods found: {result.MethodsFound}\n";
            if (result.PatternsDetected > 0)
                output += $"  • Patterns detected: {result.PatternsDetected}\n";
        }

        if (result.Errors.Any())
        {
            output += "\n⚠️ Errors/Warnings:\n";
            foreach (var error in result.Errors.Take(10))
                output += $"  • {error}\n";
            if (result.Errors.Count > 10)
                output += $"  ... and {result.Errors.Count - 10} more\n";
        }

        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #region Helpers

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

