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

            // Get all current .cs files
            var currentFiles = Directory.GetFiles(containerPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/") && 
                           !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToHashSet();

            result.TotalProcessed = currentFiles.Count;

            // Index all current files (IndexingService will also translate, but passing containerPath directly is fine)
            var indexResult = await _indexingService.IndexDirectoryAsync(containerPath, true, context, cancellationToken);
            
            result.FilesAdded = indexResult.FilesIndexed;
            result.Errors.AddRange(indexResult.Errors);

            // TODO: Implement stale file detection and removal
            // This would require tracking indexed files in the database
            // For now, we just reindex everything

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


