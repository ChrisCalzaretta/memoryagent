using System.Collections.Concurrent;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Background service that watches for file changes and automatically triggers reindex
/// </summary>
public class AutoReindexService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoReindexService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(3);
    private readonly List<FileSystemWatcher> _watchers = new();
    private Timer? _debounceTimer;

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

        var workspacePath = _configuration["AutoReindex:WorkspacePath"];
        var context = _configuration["AutoReindex:Context"];

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _logger.LogWarning("Auto-reindex enabled but WorkspacePath not configured. Skipping file watcher.");
            return;
        }

        if (!Directory.Exists(workspacePath))
        {
            _logger.LogWarning("Auto-reindex workspace path does not exist: {Path}", workspacePath);
            return;
        }

        _logger.LogInformation("🔍 Auto-reindex ENABLED");
        _logger.LogInformation("   Watching: {Path}", workspacePath);
        _logger.LogInformation("   Context: {Context}", context ?? "default");
        _logger.LogInformation("   Debounce: {Delay} seconds", _debounceDelay.TotalSeconds);

        // Setup file watchers for different file types
        var patterns = new[] { "*.cs", "*.vb", "*.cshtml", "*.razor", "*.py", "*.md", "*.css", "*.scss", "*.less", "*.js", "*.jsx", "*.ts", "*.tsx" };
        
        foreach (var pattern in patterns)
        {
            var watcher = new FileSystemWatcher(workspacePath)
            {
                Filter = pattern,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;

            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
            
            _logger.LogDebug("File watcher started for pattern: {Pattern}", pattern);
        }

        // Start debounce timer (checks every second for pending changes)
        _debounceTimer = new Timer(ProcessPendingChanges, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Filter out temporary files and unwanted paths (already filtered by reindex service, but adding here for performance)
        var filePath = e.FullPath.Replace("\\", "/");
        
        _logger.LogDebug("File change detected: {ChangeType} - {Path}", e.ChangeType, filePath);
        
        // Add to pending changes with current timestamp
        _pendingChanges.AddOrUpdate(filePath, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Handle rename as delete old + create new
        var oldPath = e.OldFullPath.Replace("\\", "/");
        var newPath = e.FullPath.Replace("\\", "/");
        
        _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", oldPath, newPath);
        
        _pendingChanges.AddOrUpdate(oldPath, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        _pendingChanges.AddOrUpdate(newPath, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    private void ProcessPendingChanges(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var filesToProcess = _pendingChanges
                .Where(kvp => now - kvp.Value >= _debounceDelay)
                .Select(kvp => kvp.Key)
                .ToList();

            if (!filesToProcess.Any())
                return;

            // Remove processed files from pending
            foreach (var file in filesToProcess)
            {
                _pendingChanges.TryRemove(file, out _);
            }

            // Trigger reindex in background (fire and forget with error handling)
            _ = Task.Run(async () =>
            {
                try
                {
                    var workspacePath = _configuration["AutoReindex:WorkspacePath"];
                    var context = _configuration["AutoReindex:Context"];

                    _logger.LogInformation("🔄 Auto-reindex triggered for {Count} file(s)", filesToProcess.Count);
                    
                    using var scope = _serviceProvider.CreateScope();
                    var reindexService = scope.ServiceProvider.GetRequiredService<IReindexService>();

                    var result = await reindexService.ReindexAsync(
                        context: context,
                        path: workspacePath,
                        removeStale: true,
                        cancellationToken: default);

                    if (result.Success)
                    {
                        _logger.LogInformation(
                            "✅ Auto-reindex completed: +{Added} -{Removed} ~{Updated} files in {Duration}s",
                            result.FilesAdded, result.FilesRemoved, result.FilesUpdated, result.Duration.TotalSeconds);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Auto-reindex completed with errors: {Errors}", 
                            string.Join(", ", result.Errors));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-reindex");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending changes");
        }
    }

    public override void Dispose()
    {
        _debounceTimer?.Dispose();
        
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        
        base.Dispose();
        _logger.LogInformation("Auto-reindex service stopped");
    }
}



