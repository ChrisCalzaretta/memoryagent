using System.Diagnostics;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for reindexing code and detecting changes
/// </summary>
public class ReindexService : IReindexService
{
    private readonly IIndexingService _indexingService;
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(
        IIndexingService indexingService,
        IVectorService vectorService,
        IGraphService graphService,
        IPathTranslationService pathTranslation,
        ILogger<ReindexService> logger)
    {
        _indexingService = indexingService;
        _vectorService = vectorService;
        _graphService = graphService;
        _pathTranslation = pathTranslation;
        _logger = logger;
    }

    public async Task<ReindexResult> ReindexAsync(
        string? context = null,
        string? path = null,
        bool removeStale = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ReindexResult { Success = true };

        try
        {
            _logger.LogInformation("Starting reindex for context: {Context}, path: {Path}", context ?? "all", path ?? "not specified");

            if (string.IsNullOrWhiteSpace(path))
            {
                result.Success = false;
                result.Errors.Add("Path is required for reindexing");
                return result;
            }

            // Translate path from host to container if needed
            var containerPath = _pathTranslation.TranslateToContainerPath(path);
            _logger.LogInformation("Reindex path translated: {Path} -> {ContainerPath}", path, containerPath);

            if (!Directory.Exists(containerPath))
            {
                result.Success = false;
                result.Errors.Add($"Directory not found: {path}");
                return result;
            }

            // Get all current code files (all supported types)
            var patterns = new[] 
            { 
                "*.cs", "*.vb", "*.cshtml", "*.razor", "*.py", "*.md", 
                "*.css", "*.scss", "*.less", 
                "*.js", "*.jsx", "*.ts", "*.tsx",
                "*.csproj", "*.vbproj", "*.fsproj", "*.sln",
                "*.json", "*.yml", "*.yaml", "*.config",
                "*.bicep",
                "Dockerfile", "*.dockerfile"
            };
            var currentFiles = patterns
                .SelectMany(pattern => Directory.GetFiles(containerPath, pattern, SearchOption.AllDirectories))
                .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/") && 
                           !f.Contains("\\obj\\") && !f.Contains("\\bin\\") &&
                           !f.Contains("/node_modules/") && !f.Contains("\\node_modules\\"))
                // Exclude .cshtml.cs and .razor.cs files (they'll be picked up by *.cs pattern)
                .Where(f => !f.EndsWith(".cshtml.cs") && !f.EndsWith(".razor.cs"))
                .Select(f => f.Replace("\\", "/")) // Normalize paths
                .ToHashSet();

            _logger.LogInformation("Found {Count} current files in {Path}", currentFiles.Count, containerPath);

            // Get all indexed files for this context from the database
            var indexedFiles = await _vectorService.GetFilePathsForContextAsync(context, cancellationToken);
            var indexedFilesSet = indexedFiles
                .Select(f => f.Replace("\\", "/")) // Normalize paths
                .ToHashSet();

            _logger.LogInformation("Found {Count} previously indexed files for context {Context}", indexedFilesSet.Count, context ?? "default");

            // Detect new files (in currentFiles but not in indexedFiles)
            var newFiles = currentFiles.Except(indexedFilesSet).ToList();
            
            // Detect deleted files (in indexedFiles but not in currentFiles)
            var deletedFiles = indexedFilesSet.Except(currentFiles).ToList();
            
            // Detect potentially modified files (exist in both - will check timestamp)
            var potentiallyModifiedFiles = currentFiles.Intersect(indexedFilesSet).ToList();

            _logger.LogInformation(
                "File detection: {New} new, {Deleted} deleted, {PotentiallyModified} potentially modified",
                newFiles.Count, deletedFiles.Count, potentiallyModifiedFiles.Count);

            result.TotalProcessed = currentFiles.Count;

            // Thread-safe counters for parallel operations
            int filesRemoved = 0;
            int filesAdded = 0;
            int filesUpdated = 0;

            // Remove deleted files (in parallel)
            if (removeStale && deletedFiles.Any())
            {
                _logger.LogInformation("Removing {Count} deleted files from index (parallel)", deletedFiles.Count);
                var deleteOptions = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = cancellationToken 
                };
                
                await Parallel.ForEachAsync(deletedFiles, deleteOptions, async (deletedFile, ct) =>
                {
                    try
                    {
                        await _vectorService.DeleteByFilePathAsync(deletedFile, context, ct);
                        await _graphService.DeleteByFilePathAsync(deletedFile, ct);
                        Interlocked.Increment(ref filesRemoved);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error removing deleted file: {File}", deletedFile);
                        lock (result.Errors)
                        {
                            result.Errors.Add($"Error removing {deletedFile}: {ex.Message}");
                        }
                    }
                });
            }

            // Index new files (in parallel)
            if (newFiles.Any())
            {
                _logger.LogInformation("Indexing {Count} new files (parallel)", newFiles.Count);
                var indexOptions = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = cancellationToken 
                };
                
                await Parallel.ForEachAsync(newFiles, indexOptions, async (newFile, ct) =>
                {
                    try
                    {
                        var fileResult = await _indexingService.IndexFileAsync(newFile, context, ct);
                        if (fileResult.Success)
                        {
                            Interlocked.Increment(ref filesAdded);
                        }
                        else
                        {
                            lock (result.Errors)
                            {
                                result.Errors.AddRange(fileResult.Errors);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error indexing new file: {File}", newFile);
                        lock (result.Errors)
                        {
                            result.Errors.Add($"Error indexing {newFile}: {ex.Message}");
                        }
                    }
                });
            }

            // Check and reindex modified files
            // For files that exist in both, check if they've been modified since last index
            if (potentiallyModifiedFiles.Any())
            {
                _logger.LogInformation("Checking {Count} files for modifications", potentiallyModifiedFiles.Count);
                
                var modifiedFiles = new List<string>();
                foreach (var file in potentiallyModifiedFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var lastModified = fileInfo.LastWriteTimeUtc;
                        
                        // Get the last indexed time from vector store metadata
                        var lastIndexed = await _vectorService.GetFileLastIndexedTimeAsync(file, cancellationToken);
                        
                        if (!lastIndexed.HasValue || lastModified > lastIndexed.Value)
                        {
                            modifiedFiles.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking modification time for: {File}", file);
                        // If we can't check, reindex it to be safe
                        modifiedFiles.Add(file);
                    }
                }

                if (modifiedFiles.Any())
                {
                    _logger.LogInformation("Reindexing {Count} modified files (parallel)", modifiedFiles.Count);
                    var reindexOptions = new ParallelOptions 
                    { 
                        MaxDegreeOfParallelism = 8,
                        CancellationToken = cancellationToken 
                    };
                    
                    await Parallel.ForEachAsync(modifiedFiles, reindexOptions, async (modifiedFile, ct) =>
                    {
                        try
                        {
                            var fileResult = await _indexingService.IndexFileAsync(modifiedFile, context, ct);
                            if (fileResult.Success)
                            {
                                Interlocked.Increment(ref filesUpdated);
                            }
                            else
                            {
                                lock (result.Errors)
                                {
                                    result.Errors.AddRange(fileResult.Errors);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reindexing modified file: {File}", modifiedFile);
                            lock (result.Errors)
                            {
                                result.Errors.Add($"Error reindexing {modifiedFile}: {ex.Message}");
                            }
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("No modified files detected");
                }
            }

            // Assign thread-safe counter values to result
            result.FilesRemoved = filesRemoved;
            result.FilesAdded = filesAdded;
            result.FilesUpdated = filesUpdated;

            result.Success = !result.Errors.Any();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Reindex completed: {Added} added, {Updated} updated, {Removed} removed in {Duration}s",
                result.FilesAdded, result.FilesUpdated, result.FilesRemoved, stopwatch.Elapsed.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reindex");
            result.Success = false;
            result.Errors.Add($"Reindex error: {ex.Message}");
            result.Duration = stopwatch.Elapsed;
            return result;
        }
    }
}


