using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for workspace management
/// Tools: register_workspace, unregister_workspace
/// </summary>
public class WorkspaceToolHandler : IMcpToolHandler
{
    private readonly IGraphService _graphService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkspaceToolHandler> _logger;

    public WorkspaceToolHandler(
        IGraphService graphService,
        IServiceProvider serviceProvider,
        ILogger<WorkspaceToolHandler> logger)
    {
        _graphService = graphService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "register_workspace",
                Description = "Register a workspace directory for file watching (auto-reindex). Called automatically by wrapper on startup.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspacePath = new { type = "string", description = "Full path to workspace directory" },
                        context = new { type = "string", description = "Context name for this workspace" }
                    },
                    required = new[] { "workspacePath", "context" }
                }
            },
            new McpTool
            {
                Name = "unregister_workspace",
                Description = "Unregister a workspace directory from file watching. Called automatically by wrapper on shutdown.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspacePath = new { type = "string", description = "Full path to workspace directory" }
                    },
                    required = new[] { "workspacePath" }
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
            "register_workspace" => await RegisterWorkspaceToolAsync(args, cancellationToken),
            "unregister_workspace" => await UnregisterWorkspaceToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> RegisterWorkspaceToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var workspacePath = args?.GetValueOrDefault("workspacePath")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("workspacePath and context are required");

        _logger.LogInformation("🔧 Registering workspace: {Path} → {Context}", workspacePath, context);

        try
        {
            // Step 1: Create isolated Qdrant collections for this workspace
            var vectorService = _serviceProvider.GetRequiredService<IVectorService>();
            await vectorService.InitializeCollectionsForContextAsync(context, cancellationToken);
            _logger.LogInformation("  ✅ Qdrant collections created for: {Context}", context);

            // Step 2: Create isolated Neo4j database for this workspace
            await _graphService.CreateDatabaseAsync(context, cancellationToken);
            _logger.LogInformation("  ✅ Neo4j database created for: {Context}", context);

            // Step 3: Register file watcher for auto-reindex
            var autoReindexService = _serviceProvider.GetService<FileWatcher.IAutoReindexService>();
            if (autoReindexService != null)
            {
                await autoReindexService.RegisterWorkspaceAsync(workspacePath, context);
                _logger.LogInformation("  ✅ File watcher started for: {Context}", context);
            }
            else
            {
                _logger.LogWarning("  ⚠️ AutoReindexService not available - file watching disabled");
            }

            // Step 4: Check if workspace is empty - if so, trigger initial full reindex
            var filePaths = await vectorService.GetFilePathsForContextAsync(context, cancellationToken);
            if (!filePaths.Any())
            {
                _logger.LogInformation("  🔍 Collections empty, triggering initial full reindex...");
                
                // Trigger background reindex (don't wait for it)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var indexingService = _serviceProvider.GetRequiredService<IIndexingService>();
                        await indexingService.IndexDirectoryAsync(
                            workspacePath,
                            true, // recursive
                            context,
                            CancellationToken.None
                        );
                        _logger.LogInformation("  ✅ Initial reindex completed for: {Context}", context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "  ❌ Initial reindex failed for: {Context}", context);
                    }
                }, CancellationToken.None);

                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = $"✅ Workspace registered with isolated storage:\n" +
                                   $"  Path: {workspacePath}\n" +
                                   $"  Context: {context}\n" +
                                   $"  Qdrant Collections: {context.ToLower()}_files, {context.ToLower()}_classes, {context.ToLower()}_methods, {context.ToLower()}_patterns\n" +
                                   $"  Neo4j Database: {context.ToLower()}\n" +
                                   $"  File Watcher: {(autoReindexService != null ? "Active" : "Disabled")}\n" +
                                   $"\n🔄 Initial indexing started in background... This may take a few minutes."
                        }
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
                        Text = $"✅ Workspace registered with isolated storage:\n" +
                               $"  Path: {workspacePath}\n" +
                               $"  Context: {context}\n" +
                               $"  Qdrant Collections: {context.ToLower()}_files, {context.ToLower()}_classes, {context.ToLower()}_methods, {context.ToLower()}_patterns\n" +
                               $"  Neo4j Database: {context.ToLower()}\n" +
                               $"  File Watcher: {(autoReindexService != null ? "Active" : "Disabled")}\n" +
                               $"  Indexed Files: {filePaths.Count}"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering workspace: {Context}", context);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"❌ Error registering workspace: {ex.Message}"
                    }
                }
            };
        }
    }

    private async Task<McpToolResult> UnregisterWorkspaceToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var workspacePath = args?.GetValueOrDefault("workspacePath")?.ToString();

        if (string.IsNullOrWhiteSpace(workspacePath))
            return ErrorResult("workspacePath is required");

        var autoReindexService = _serviceProvider.GetService<FileWatcher.IAutoReindexService>();
        if (autoReindexService == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "⚠️ AutoReindexService not available - no file watchers to unregister"
                    }
                }
            };
        }

        await autoReindexService.UnregisterWorkspaceAsync(workspacePath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Workspace unregistered:\n  Path: {workspacePath}\n  File watcher stopped."
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

