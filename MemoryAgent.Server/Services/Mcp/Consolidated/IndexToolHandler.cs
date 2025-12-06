using System.Collections.Concurrent;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexToolHandler> _logger;

    // Track background indexing jobs
    private static readonly ConcurrentDictionary<string, BackgroundIndexJob> _backgroundJobs = new();

    public IndexToolHandler(
        IIndexingService indexingService,
        IReindexService reindexService,
        IServiceProvider serviceProvider,
        ILogger<IndexToolHandler> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public class BackgroundIndexJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Context { get; set; } = "";
        public string Path { get; set; } = "";
        public string Scope { get; set; } = "";
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Running"; // Running, Completed, Failed
        public int FilesProcessed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "index",
                Description = "Index code into memory. Use scope='file' for single file, scope='directory' for folder, scope='reindex' to update after changes. Use background=true for large projects to avoid timeout.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to file or directory to index" },
                        context = new { type = "string", description = "Project context name" },
                        scope = new { type = "string", description = "Index scope: 'file' (single file), 'directory' (folder), 'reindex' (update changes)", @default = "file", @enum = new[] { "file", "directory", "reindex" } },
                        recursive = new { type = "boolean", description = "For directory scope: index subdirectories (default: true)", @default = true },
                        removeStale = new { type = "boolean", description = "For reindex scope: remove deleted files from memory (default: true)", @default = true },
                        background = new { type = "boolean", description = "Run in background to avoid timeout on large projects (default: false). Use for directory/reindex on large codebases.", @default = false },
                        jobId = new { type = "string", description = "Check status of a background job by ID (use with scope='status')" }
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
        var background = SafeParseBool(args?.GetValueOrDefault("background"), false);
        var jobId = args?.GetValueOrDefault("jobId")?.ToString();

        // Check status of background job
        if (scope == "status" || !string.IsNullOrEmpty(jobId))
        {
            return GetBackgroundJobStatus(jobId, context);
        }

        if (string.IsNullOrWhiteSpace(path))
            return ErrorResult("path is required");
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        // For large operations, use background mode
        if (background && (scope == "directory" || scope == "reindex"))
        {
            return StartBackgroundIndex(path, context, scope, args);
        }

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

    private McpToolResult StartBackgroundIndex(string path, string context, string scope, Dictionary<string, object>? args)
    {
        var job = new BackgroundIndexJob
        {
            Context = context,
            Path = path,
            Scope = scope
        };

        _backgroundJobs[job.Id] = job;
        _logger.LogInformation("🚀 Starting background index job {JobId} for {Context}: {Scope} at {Path}", 
            job.Id, context, scope, path);

        // Start background task
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope2 = _serviceProvider.CreateScope();
                var indexingService = scope2.ServiceProvider.GetRequiredService<IIndexingService>();
                var reindexService = scope2.ServiceProvider.GetRequiredService<IReindexService>();

                if (scope == "directory")
                {
                    var recursive = SafeParseBool(args?.GetValueOrDefault("recursive"), true);
                    var result = await indexingService.IndexDirectoryAsync(path, recursive, context, CancellationToken.None);
                    job.FilesProcessed = result.FilesIndexed;
                    job.Errors = result.Errors;
                    job.Status = result.Success ? "Completed" : "Failed";
                }
                else if (scope == "reindex")
                {
                    var removeStale = SafeParseBool(args?.GetValueOrDefault("removeStale"), true);
                    var result = await reindexService.ReindexAsync(context, path, removeStale, CancellationToken.None);
                    job.FilesProcessed = result.FilesAdded + result.FilesUpdated;
                    job.Errors = result.Errors;
                    job.Status = result.Success ? "Completed" : "Failed";
                }

                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("✅ Background job {JobId} completed: {Files} files processed", 
                    job.Id, job.FilesProcessed);
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Errors.Add(ex.Message);
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "❌ Background job {JobId} failed", job.Id);
            }
        });

        var output = $"🚀 Background Index Started\n\n";
        output += $"Job ID: {job.Id}\n";
        output += $"Context: {context}\n";
        output += $"Scope: {scope}\n";
        output += $"Path: {path}\n";
        output += $"Started: {job.StartedAt:HH:mm:ss}\n\n";
        output += $"💡 Check status with:\n";
        output += $"   index(scope=\"status\", jobId=\"{job.Id}\", path=\".\", context=\"{context}\")\n\n";
        output += $"The indexing will continue even if this chat times out.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private McpToolResult GetBackgroundJobStatus(string? jobId, string? context)
    {
        // List all jobs for context if no specific jobId
        if (string.IsNullOrEmpty(jobId))
        {
            var contextJobs = _backgroundJobs.Values
                .Where(j => string.IsNullOrEmpty(context) || j.Context == context)
                .OrderByDescending(j => j.StartedAt)
                .Take(10)
                .ToList();

            if (!contextJobs.Any())
            {
                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = "📋 No background indexing jobs found." }
                    }
                };
            }

            var output = $"📋 Background Index Jobs\n";
            output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

            foreach (var j in contextJobs)
            {
                var statusIcon = j.Status switch
                {
                    "Running" => "🔄",
                    "Completed" => "✅",
                    "Failed" => "❌",
                    _ => "❓"
                };
                var duration = (j.CompletedAt ?? DateTime.UtcNow) - j.StartedAt;
                
                output += $"{statusIcon} Job {j.Id} ({j.Status})\n";
                output += $"   Context: {j.Context}, Scope: {j.Scope}\n";
                output += $"   Files: {j.FilesProcessed}, Duration: {duration:mm\\:ss}\n";
                if (j.Errors.Any())
                    output += $"   Errors: {j.Errors.Count}\n";
                output += "\n";
            }

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = output }
                }
            };
        }

        // Get specific job
        if (!_backgroundJobs.TryGetValue(jobId, out var job))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"❌ Job not found: {jobId}" }
                }
            };
        }

        var statusEmoji = job.Status switch
        {
            "Running" => "🔄",
            "Completed" => "✅",
            "Failed" => "❌",
            _ => "❓"
        };
        var dur = (job.CompletedAt ?? DateTime.UtcNow) - job.StartedAt;

        var result = $"{statusEmoji} Background Index Job: {job.Id}\n";
        result += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        result += $"Status: {job.Status}\n";
        result += $"Context: {job.Context}\n";
        result += $"Scope: {job.Scope}\n";
        result += $"Path: {job.Path}\n";
        result += $"Started: {job.StartedAt:HH:mm:ss}\n";
        if (job.CompletedAt.HasValue)
            result += $"Completed: {job.CompletedAt:HH:mm:ss}\n";
        result += $"Duration: {dur:mm\\:ss}\n";
        result += $"Files Processed: {job.FilesProcessed}\n";

        if (job.Errors.Any())
        {
            result += $"\n⚠️ Errors ({job.Errors.Count}):\n";
            foreach (var err in job.Errors.Take(5))
                result += $"  • {err}\n";
            if (job.Errors.Count > 5)
                result += $"  ... and {job.Errors.Count - 5} more\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = result }
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

