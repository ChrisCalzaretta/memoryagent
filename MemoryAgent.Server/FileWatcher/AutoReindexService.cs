using System.Collections.Concurrent;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Background service that watches for file changes across multiple workspaces
/// and automatically triggers reindex per project context
/// </summary>
public class AutoReindexService : BackgroundService, IAutoReindexService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoReindexService> _logger;
    private readonly IConfiguration _configuration;
    
    // Multi-workspace support
    private readonly ConcurrentDictionary<string, WorkspaceWatcher> _activeWatchers = new();
    private readonly ConcurrentDictionary<string, FileChange> _pendingChanges = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(3);
    private Timer? _debounceTimer;
    private Timer? _cleanupTimer;

    public class WorkspaceWatcher
    {
        public string WorkspacePath { get; set; } = "";
        public string Context { get; set; } = "";
        public List<FileSystemWatcher> Watchers { get; set; } = new();
        public DateTime LastActivity { get; set; }
    }

    public class FileChange
    {
        public DateTime Timestamp { get; set; }
        public string Context { get; set; } = "";
    }

    public AutoReindexService(
        IServiceProvider serviceProvider,
        ILogger<AutoReindexService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if auto-reindex is enabled
        var enabled = _configuration.GetValue<bool>("AutoReindex:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("Auto-reindex is DISABLED. Set AutoReindex:Enabled=true to enable.");
            return;
        }

        _logger.LogInformation("🔍 Auto-reindex service READY (multi-workspace mode)");
        _logger.LogInformation("   Waiting for workspace registrations from Cursor...");
        _logger.LogInformation("   Debounce delay: {Delay} seconds", _debounceDelay.TotalSeconds);

        // Start debounce timer (checks every second for pending changes)
        _debounceTimer = new Timer(ProcessPendingChanges, null, 
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Start cleanup timer (remove stale watchers every 10 minutes)
        _cleanupTimer = new Timer(CleanupStaleWatchers, null, 
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public Task RegisterWorkspaceAsync(string workspacePath, string context)
    {
        // Normalize path and convert to container path
        // Input: "E:\\GitHub\\MemoryAgent" or "E:\GitHub\MemoryAgent"
        // Output: "/workspace/MemoryAgent"
        workspacePath = workspacePath.Replace("\\\\", "\\").Replace("\\", "/");
        
        // Convert Windows path to container path
        // E:/GitHub/MemoryAgent -> /workspace/MemoryAgent
        if (workspacePath.Contains(":/"))
        {
            var parts = workspacePath.Split('/');
            var projectFolder = parts[parts.Length - 1]; // "MemoryAgent"
            workspacePath = $"/workspace/{projectFolder}";
        }
        
        if (_activeWatchers.ContainsKey(workspacePath))
        {
            _logger.LogInformation("Workspace already registered, updating activity: {Path}", workspacePath);
            _activeWatchers[workspacePath].LastActivity = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        if (!Directory.Exists(workspacePath))
        {
            _logger.LogWarning("Workspace path does not exist: {Path}", workspacePath);
            _logger.LogInformation("Note: This is expected if using container paths. Available directories in /workspace:");
            try
            {
                if (Directory.Exists("/workspace"))
                {
                    foreach (var dir in Directory.GetDirectories("/workspace"))
                    {
                        _logger.LogInformation("  - {Dir}", dir);
                    }
                }
            }
            catch { }
            return Task.CompletedTask;
        }

        _logger.LogInformation("🔍 Starting file watcher: {Path} → {Context}", 
            workspacePath, context);

        var watchers = new List<FileSystemWatcher>();
        var patterns = new[] 
        { 
            "*.cs", "*.vb", "*.cshtml", "*.razor", 
            "*.py", "*.md", 
            "*.dart",  // Flutter/Dart support
            "*.css", "*.scss", "*.less", 
            "*.js", "*.jsx", "*.ts", "*.tsx",
            "*.json", "*.yml", "*.yaml"
        };

        foreach (var pattern in patterns)
        {
            try
            {
                var watcher = new FileSystemWatcher(workspacePath)
                {
                    Filter = pattern,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                // Capture context in closure
                var capturedContext = context;
                var capturedPath = workspacePath;
                
                watcher.Changed += (s, e) => OnFileChanged(s, e, capturedContext, capturedPath);
                watcher.Created += (s, e) => OnFileChanged(s, e, capturedContext, capturedPath);
                watcher.Deleted += (s, e) => OnFileChanged(s, e, capturedContext, capturedPath);
                watcher.Renamed += (s, e) => OnFileRenamed(s, e, capturedContext, capturedPath);

                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
                
                _logger.LogDebug("  ✓ Watching {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create watcher for pattern {Pattern}", pattern);
            }
        }

        _activeWatchers[workspacePath] = new WorkspaceWatcher
        {
            WorkspacePath = workspacePath,
            Context = context,
            Watchers = watchers,
            LastActivity = DateTime.UtcNow
        };

        _logger.LogInformation("✅ File watcher started: {Path} ({Count} patterns monitored)", 
            workspacePath, patterns.Length);
        
        return Task.CompletedTask;
    }

    public Task UnregisterWorkspaceAsync(string workspacePath)
    {
        workspacePath = workspacePath.Replace("\\", "/");
        
        if (_activeWatchers.TryRemove(workspacePath, out var watcher))
        {
            foreach (var w in watcher.Watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }

            _logger.LogInformation("🛑 File watcher stopped: {Path}", workspacePath);
        }
        else
        {
            _logger.LogDebug("Workspace not registered: {Path}", workspacePath);
        }
        
        return Task.CompletedTask;
    }

    public List<string> GetRegisteredWorkspaces()
    {
        return _activeWatchers.Keys.ToList();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e, string context, string workspacePath)
    {
        var filePath = e.FullPath.Replace("\\", "/");
        
        // Filter out common temp/build files
        if (ShouldIgnoreFile(filePath))
        {
            return;
        }
        
        _logger.LogDebug("File {ChangeType} in {Context}: {Path}", 
            e.ChangeType, context, Path.GetFileName(filePath));
        
        // Update workspace activity
        if (_activeWatchers.TryGetValue(workspacePath, out var workspace))
        {
            workspace.LastActivity = DateTime.UtcNow;
        }
        
        // Add to pending changes
        _pendingChanges.AddOrUpdate(filePath, new FileChange
        {
            Timestamp = DateTime.UtcNow,
            Context = context
        }, (_, _) => new FileChange
        {
            Timestamp = DateTime.UtcNow,
            Context = context
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e, string context, string workspacePath)
    {
        var oldPath = e.OldFullPath.Replace("\\", "/");
        var newPath = e.FullPath.Replace("\\", "/");
        
        if (ShouldIgnoreFile(oldPath) && ShouldIgnoreFile(newPath))
        {
            return;
        }
        
        _logger.LogDebug("File renamed in {Context}: {OldName} → {NewName}", 
            context, Path.GetFileName(oldPath), Path.GetFileName(newPath));
        
        // Update workspace activity
        if (_activeWatchers.TryGetValue(workspacePath, out var workspace))
        {
            workspace.LastActivity = DateTime.UtcNow;
        }
        
        var now = DateTime.UtcNow;
        _pendingChanges.AddOrUpdate(oldPath, new FileChange { Timestamp = now, Context = context }, 
            (_, _) => new FileChange { Timestamp = now, Context = context });
        _pendingChanges.AddOrUpdate(newPath, new FileChange { Timestamp = now, Context = context },
            (_, _) => new FileChange { Timestamp = now, Context = context });
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var directory = Path.GetDirectoryName(filePath)?.Replace("\\", "/") ?? "";
        
        // Ignore temp files
        if (fileName.StartsWith(".") || fileName.EndsWith(".tmp") || fileName.EndsWith(".swp"))
            return true;
        
        // Ignore build/output directories
        if (directory.Contains("/bin/") || directory.Contains("/obj/") || 
            directory.Contains("/node_modules/") || directory.Contains("/.git/") ||
            directory.Contains("/dist/") || directory.Contains("/build/"))
            return true;
        
        return false;
    }

    private void ProcessPendingChanges(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var changesToProcess = _pendingChanges
                .Where(kvp => now - kvp.Value.Timestamp >= _debounceDelay)
                .ToList();

            if (!changesToProcess.Any())
                return;

            // Remove processed files from pending
            foreach (var change in changesToProcess)
            {
                _pendingChanges.TryRemove(change.Key, out _);
            }

            // Group by context (project)
            var byContext = changesToProcess
                .GroupBy(c => c.Value.Context)
                .ToList();

            foreach (var group in byContext)
            {
                var context = group.Key;
                var files = group.Select(c => c.Key).ToList();
                
                // Find workspace path for this context
                var workspace = _activeWatchers.Values
                    .FirstOrDefault(w => w.Context == context);
                
                if (workspace == null)
                {
                    _logger.LogWarning("No workspace found for context: {Context}", context);
                    continue;
                }

                // Trigger reindex in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("🔄 Auto-reindex triggered for {Context}: {Count} file(s)", 
                            context, files.Count);

                        using var scope = _serviceProvider.CreateScope();
                        var reindexService = scope.ServiceProvider.GetRequiredService<IReindexService>();

                        var result = await reindexService.ReindexAsync(
                            context: context,
                            path: workspace.WorkspacePath,
                            removeStale: true,
                            cancellationToken: default);

                        if (result.Success)
                        {
                            _logger.LogInformation(
                                "✅ Auto-reindex completed for {Context}: +{Added} -{Removed} ~{Updated} files in {Duration:F1}s",
                                context, result.FilesAdded, result.FilesRemoved, 
                                result.FilesUpdated, result.Duration.TotalSeconds);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Auto-reindex completed with errors for {Context}: {Errors}", 
                                context, string.Join(", ", result.Errors));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during auto-reindex for {Context}", context);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending changes");
        }
    }

    private void CleanupStaleWatchers(object? state)
    {
        try
        {
            var staleTimeout = TimeSpan.FromMinutes(30); // Increased to 30 minutes
            var now = DateTime.UtcNow;

            var staleWatchers = _activeWatchers
                .Where(kvp => now - kvp.Value.LastActivity > staleTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var path in staleWatchers)
            {
                _logger.LogWarning("Removing stale watcher (no activity for 30 min): {Path}", path);
                _ = UnregisterWorkspaceAsync(path);
            }

            if (_activeWatchers.Any())
            {
                _logger.LogDebug("Active watchers: {Count}", _activeWatchers.Count);
                foreach (var kvp in _activeWatchers)
                {
                    var timeSinceActivity = now - kvp.Value.LastActivity;
                    _logger.LogDebug("  • {Context}: {Path} (last activity: {Minutes:F0}m ago)",
                        kvp.Value.Context, kvp.Key, timeSinceActivity.TotalMinutes);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of stale watchers");
        }
    }

    public override void Dispose()
    {
        _debounceTimer?.Dispose();
        _cleanupTimer?.Dispose();

        foreach (var watcher in _activeWatchers.Values)
        {
            foreach (var w in watcher.Watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
        }

        _activeWatchers.Clear();
        base.Dispose();
        
        _logger.LogInformation("Auto-reindex service stopped");
    }
}
