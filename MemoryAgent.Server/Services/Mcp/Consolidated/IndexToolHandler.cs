using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for all indexing operations.
/// Tool: index
/// Consolidates: index_file, index_directory, reindex
/// Uses a queue to process one indexing job at a time (searches still work concurrently)
/// </summary>
public class IndexToolHandler : IMcpToolHandler
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexToolHandler> _logger;

    // Track background indexing jobs
    private static readonly ConcurrentDictionary<string, BackgroundIndexJob> _backgroundJobs = new();
    
    // Queue for serializing indexing jobs (only one runs at a time)
    private static readonly ConcurrentQueue<BackgroundIndexJob> _jobQueue = new();
    private static readonly SemaphoreSlim _indexingSemaphore = new(1, 1);  // Only 1 job at a time
    private static bool _queueProcessorRunning = false;
    private static readonly object _processorLock = new();

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
        private int _filesProcessed;

        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Context { get; set; } = "";
        public string Path { get; set; } = "";
        public string Scope { get; set; } = "";
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Queued"; // Queued, Running, Completed, Failed
        public int QueuePosition { get; set; } = 0;
        public int FilesProcessed
        {
            get => _filesProcessed;
            set => _filesProcessed = value;
        }
        public List<string> Errors { get; set; } = new();
        
        // Store args for deferred execution
        public Dictionary<string, object>? Args { get; set; }
        
        // Cancellation support
        public CancellationTokenSource CancellationSource { get; } = new();
        public bool IsCancelled => CancellationSource.IsCancellationRequested;

        public void IncrementProcessed() => Interlocked.Increment(ref _filesProcessed);
        
        public void Cancel()
        {
            CancellationSource.Cancel();
            Status = "Cancelled";
        }
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
                        scope = new { type = "string", description = "Index scope: 'file' (single file), 'directory' (folder), 'reindex' (update changes), 'status' (check job status), 'cancel' (cancel a job)", @default = "file", @enum = new[] { "file", "directory", "reindex", "status", "cancel" } },
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
        if (scope == "status" || (!string.IsNullOrEmpty(jobId) && scope != "cancel"))
        {
            return GetBackgroundJobStatus(jobId, context);
        }

        // Cancel a background job
        if (scope == "cancel")
        {
            return CancelBackgroundJob(jobId);
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
                var reindexResult = await _reindexService.ReindexAsync(
                    context: context,
                    path: path,
                    removeStale: removeStale,
                    cancellationToken: ct);
                // Map ReindexResult to IndexResult for unified output
                result = new IndexResult 
                { 
                    Success = reindexResult.Success, 
                    FilesIndexed = reindexResult.FilesAdded + reindexResult.FilesUpdated + reindexResult.FilesRemoved,
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
        // Calculate queue position
        var queuePosition = _jobQueue.Count + (_backgroundJobs.Values.Any(j => j.Status == "Running") ? 1 : 0);
        
        var job = new BackgroundIndexJob
        {
            Context = context,
            Path = path,
            Scope = scope,
            Args = args,
            QueuePosition = queuePosition,
            Status = queuePosition == 0 ? "Running" : "Queued"
        };

        _backgroundJobs[job.Id] = job;
        _jobQueue.Enqueue(job);
        
        _logger.LogInformation("📋 Queued background index job {JobId} for {Context}: {Scope} at {Path} (position: {Position})", 
            job.Id, context, scope, path, queuePosition);

        // Start queue processor if not running
        EnsureQueueProcessorRunning();

        var output = queuePosition == 0 
            ? $"🚀 Background Index Started\n\n"
            : $"📋 Background Index Queued (Position: {queuePosition})\n\n";
        output += $"Job ID: {job.Id}\n";
        output += $"Context: {context}\n";
        output += $"Scope: {scope}\n";
        output += $"Path: {path}\n";
        output += $"Queued: {job.QueuedAt:HH:mm:ss}\n";
        if (queuePosition > 0)
            output += $"Queue Position: {queuePosition} (will start after current job completes)\n";
        output += $"\n💡 Check status with:\n";
        output += $"   index(scope=\"status\", jobId=\"{job.Id}\", path=\".\", context=\"{context}\")\n\n";
        output += $"The indexing will continue even if this chat times out.\n";
        output += $"⚡ Searches and queries work while indexing runs.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private void EnsureQueueProcessorRunning()
    {
        lock (_processorLock)
        {
            if (_queueProcessorRunning) return;
            _queueProcessorRunning = true;
            
            _ = Task.Run(ProcessJobQueueAsync);
        }
    }

    private async Task ProcessJobQueueAsync()
    {
        _logger.LogInformation("🔄 Index job queue processor started");
        
        try
        {
            while (_jobQueue.TryDequeue(out var job))
            {
                // Wait for semaphore - only one job at a time
                await _indexingSemaphore.WaitAsync();
                
                try
                {
                    // Skip if job was cancelled while queued
                    if (job.IsCancelled)
                    {
                        _logger.LogInformation("⏭️ Skipping cancelled job {JobId}", job.Id);
                        continue;
                    }
                    
                    job.Status = "Running";
                    job.StartedAt = DateTime.UtcNow;
                    job.QueuePosition = 0;
                    
                    // Update queue positions for remaining jobs
                    var position = 1;
                    foreach (var queuedJob in _jobQueue)
                    {
                        queuedJob.QueuePosition = position++;
                    }
                    
                    _logger.LogInformation("🚀 Starting queued job {JobId} for {Context}: {Scope}", 
                        job.Id, job.Context, job.Scope);

                    using var scope = _serviceProvider.CreateScope();
                    var indexingService = scope.ServiceProvider.GetRequiredService<IIndexingService>();
                    var reindexService = scope.ServiceProvider.GetRequiredService<IReindexService>();

                    if (job.Scope == "directory")
                    {
                        var recursive = SafeParseBool(job.Args?.GetValueOrDefault("recursive"), true);
                        var result = await indexingService.IndexDirectoryAsync(
                            job.Path, 
                            recursive, 
                            job.Context, 
                            job.CancellationSource.Token,  // Use job's cancellation token
                            progressCallback: _ => job.IncrementProcessed());
                        job.FilesProcessed = result.FilesIndexed;
                        job.Errors = result.Errors;
                        job.Status = job.IsCancelled ? "Cancelled" : (result.Success ? "Completed" : "Failed");
                    }
                    else if (job.Scope == "reindex")
                    {
                        var removeStale = SafeParseBool(job.Args?.GetValueOrDefault("removeStale"), true);
                        var result = await reindexService.ReindexAsync(
                            context: job.Context,
                            path: job.Path,
                            removeStale: removeStale,
                            cancellationToken: job.CancellationSource.Token,  // Use job's cancellation token
                            progressCallback: _ => job.IncrementProcessed());
                        job.FilesProcessed = result.FilesAdded + result.FilesUpdated + result.FilesRemoved;
                        job.Errors = result.Errors;
                        job.Status = job.IsCancelled ? "Cancelled" : (result.Success ? "Completed" : "Failed");
                    }

                    job.CompletedAt = DateTime.UtcNow;
                    var statusEmoji = job.Status == "Cancelled" ? "🛑" : "✅";
                    _logger.LogInformation("{Emoji} Background job {JobId} {Status}: {Files} files processed", 
                        statusEmoji, job.Id, job.Status.ToLower(), job.FilesProcessed);
                }
                catch (OperationCanceledException)
                {
                    job.Status = "Cancelled";
                    job.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("🛑 Background job {JobId} cancelled: {Files} files processed", 
                        job.Id, job.FilesProcessed);
                }
                catch (Exception ex)
                {
                    job.Status = job.IsCancelled ? "Cancelled" : "Failed";
                    job.Errors.Add(ex.Message);
                    job.CompletedAt = DateTime.UtcNow;
                    _logger.LogError(ex, "❌ Background job {JobId} failed", job.Id);
                }
                finally
                {
                    _indexingSemaphore.Release();
                }
            }
        }
        finally
        {
            lock (_processorLock)
            {
                _queueProcessorRunning = false;
            }
            _logger.LogInformation("🔄 Index job queue processor stopped (queue empty)");
        }
    }

    private McpToolResult CancelBackgroundJob(string? jobId)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "❌ jobId is required to cancel a job.\n\nUsage: index(scope=\"cancel\", jobId=\"abc123\", path=\".\", context=\"x\")" }
                }
            };
        }

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

        if (job.Status == "Completed" || job.Status == "Failed" || job.Status == "Cancelled")
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"⚠️ Job {jobId} already {job.Status.ToLower()} - cannot cancel." }
                }
            };
        }

        var wasRunning = job.Status == "Running";
        job.Cancel();
        job.CompletedAt = DateTime.UtcNow;
        
        _logger.LogInformation("🛑 Cancelled background job {JobId} (was {Status})", jobId, wasRunning ? "running" : "queued");

        var output = $"🛑 Job Cancelled\n";
        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        output += $"Job ID: {job.Id}\n";
        output += $"Context: {job.Context}\n";
        output += $"Was: {(wasRunning ? "Running" : "Queued")}\n";
        output += $"Files Processed: {job.FilesProcessed}\n";
        
        if (wasRunning)
            output += $"\n⚠️ Note: The job may take a moment to fully stop.\n";

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
                .OrderBy(j => j.Status == "Running" ? 0 : j.Status == "Queued" ? 1 : 2)  // Running first, then Queued, then others
                .ThenBy(j => j.QueuePosition)  // By queue position for queued jobs
                .ThenByDescending(j => j.QueuedAt)  // Most recent first for completed
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
                    "Queued" => "⏳",
                    "Running" => "🔄",
                    "Completed" => "✅",
                    "Cancelled" => "🛑",
                    "Failed" => "❌",
                    _ => "❓"
                };
                var duration = j.StartedAt.HasValue 
                    ? (j.CompletedAt ?? DateTime.UtcNow) - j.StartedAt.Value
                    : TimeSpan.Zero;
                
                output += $"{statusIcon} Job {j.Id} ({j.Status})\n";
                output += $"   Context: {j.Context}, Scope: {j.Scope}\n";
                if (j.Status == "Queued")
                    output += $"   Queue Position: {j.QueuePosition}\n";
                else
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
            "Queued" => "⏳",
            "Running" => "🔄",
            "Completed" => "✅",
            "Cancelled" => "🛑",
            "Failed" => "❌",
            _ => "❓"
        };

        var result = $"{statusEmoji} Background Index Job: {job.Id}\n";
        result += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        result += $"Status: {job.Status}\n";
        result += $"Context: {job.Context}\n";
        result += $"Scope: {job.Scope}\n";
        result += $"Path: {job.Path}\n";
        result += $"Queued: {job.QueuedAt:HH:mm:ss}\n";
        
        if (job.Status == "Queued")
        {
            result += $"Queue Position: {job.QueuePosition}\n";
            result += $"⏳ Waiting for current job to complete...\n";
        }
        else
        {
            if (job.StartedAt.HasValue)
                result += $"Started: {job.StartedAt:HH:mm:ss}\n";
            if (job.CompletedAt.HasValue)
                result += $"Completed: {job.CompletedAt:HH:mm:ss}\n";
            
            var dur = job.StartedAt.HasValue 
                ? (job.CompletedAt ?? DateTime.UtcNow) - job.StartedAt.Value
                : TimeSpan.Zero;
            result += $"Duration: {dur:mm\\:ss}\n";
            result += $"Files Processed: {job.FilesProcessed}\n";
        }

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

