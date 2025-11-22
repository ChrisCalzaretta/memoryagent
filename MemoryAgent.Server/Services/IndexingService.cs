using System.Diagnostics;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Orchestrates code indexing across Qdrant, Neo4j, and Ollama
/// </summary>
public class IndexingService : IIndexingService
{
    private readonly ICodeParser _codeParser;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<IndexingService> _logger;

    public IndexingService(
        ICodeParser codeParser,
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        IGraphService graphService,
        IPathTranslationService pathTranslation,
        ILogger<IndexingService> logger)
    {
        _codeParser = codeParser;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _graphService = graphService;
        _pathTranslation = pathTranslation;
        _logger = logger;
    }

    public async Task<IndexResult> IndexFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IndexResult();

        try
        {
            // Translate path from host to container if needed
            var containerPath = _pathTranslation.TranslateToContainerPath(filePath);
            _logger.LogInformation("Indexing file: {FilePath} (translated to: {ContainerPath})", filePath, containerPath);

            // Step 0: Delete existing data for this file (if any) to avoid duplicates
            _logger.LogInformation("Checking for existing data for file: {FilePath}", containerPath);
            await Task.WhenAll(
                _vectorService.DeleteByFilePathAsync(containerPath, cancellationToken),
                _graphService.DeleteByFilePathAsync(containerPath, cancellationToken)
            );

            // Step 1: Parse the file
            var parseResult = await _codeParser.ParseFileAsync(containerPath, context, cancellationToken);
            if (!parseResult.Success)
            {
                result.Errors.AddRange(parseResult.Errors);
                return result;
            }

            // Step 2: Generate embeddings for all code elements
            var textsToEmbed = parseResult.CodeElements.Select(e => e.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);

            // Assign embeddings to code elements
            for (int i = 0; i < parseResult.CodeElements.Count; i++)
            {
                parseResult.CodeElements[i].Embedding = embeddings[i];
            }

            // Step 3: Store in parallel (Qdrant + Neo4j)
            var storeVectorTask = _vectorService.StoreCodeMemoriesAsync(parseResult.CodeElements, cancellationToken);
            var storeGraphTask = _graphService.StoreCodeNodesAsync(parseResult.CodeElements, cancellationToken);

            await Task.WhenAll(storeVectorTask, storeGraphTask);

            // Step 4: Create relationships in Neo4j
            if (parseResult.Relationships.Any())
            {
                await _graphService.CreateRelationshipsAsync(parseResult.Relationships, cancellationToken);
            }

            // Update result
            result.Success = true;
            result.FilesIndexed = 1;
            result.ClassesFound = parseResult.CodeElements.Count(e => e.Type == CodeMemoryType.Class);
            result.MethodsFound = parseResult.CodeElements.Count(e => e.Type == CodeMemoryType.Method);
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Successfully indexed {FilePath}: {Classes} classes, {Methods} methods in {Duration}ms",
                filePath, result.ClassesFound, result.MethodsFound, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing file: {FilePath}", filePath);
            result.Errors.Add($"Error indexing file: {ex.Message}");
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<IndexResult> IndexDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IndexResult { Success = true };

        try
        {
            // Translate path from host to container if needed
            var containerPath = _pathTranslation.TranslateToContainerPath(directoryPath);
            _logger.LogInformation("Indexing directory: {DirectoryPath} (translated to: {ContainerPath}, recursive: {Recursive})", 
                directoryPath, containerPath, recursive);

            if (!Directory.Exists(containerPath))
            {
                result.Success = false;
                result.Errors.Add($"Directory not found: {containerPath} (original: {directoryPath})");
                return result;
            }

            // Find all supported code files (.cs, .cshtml, .razor, .py, .md)
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var patterns = new[] { "*.cs", "*.cshtml", "*.razor", "*.py", "*.md" };
            
            var codeFiles = patterns
                .SelectMany(pattern => Directory.GetFiles(containerPath, pattern, searchOption))
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.Contains("/obj/") && !f.Contains("/bin/"))
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} code files to index ({CSharp} .cs, {Razor} .cshtml/.razor, {Python} .py, {Markdown} .md)", 
                codeFiles.Count,
                codeFiles.Count(f => f.EndsWith(".cs")),
                codeFiles.Count(f => f.EndsWith(".cshtml") || f.EndsWith(".razor")),
                codeFiles.Count(f => f.EndsWith(".py")),
                codeFiles.Count(f => f.EndsWith(".md")));

            // Index files in parallel (but limit concurrency to avoid overwhelming services)
            var semaphore = new SemaphoreSlim(5); // Max 5 concurrent file indexes
            var indexTasks = codeFiles.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await IndexFileAsync(file, context, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var fileResults = await Task.WhenAll(indexTasks);

            // Aggregate results
            foreach (var fileResult in fileResults)
            {
                result.FilesIndexed += fileResult.FilesIndexed;
                result.ClassesFound += fileResult.ClassesFound;
                result.MethodsFound += fileResult.MethodsFound;
                result.Errors.AddRange(fileResult.Errors);
            }

            result.Success = !result.Errors.Any();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Directory indexing completed: {Files} files, {Classes} classes, {Methods} methods in {Duration}s",
                result.FilesIndexed, result.ClassesFound, result.MethodsFound, stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing directory: {DirectoryPath}", directoryPath);
            result.Success = false;
            result.Errors.Add($"Error indexing directory: {ex.Message}");
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<QueryResult> QueryAsync(
        string query,
        string? context = null,
        int limit = 5,
        float minimumScore = 0.5f,  // Lowered from 0.7 for better results
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Querying: {Query} (context: {Context})", query, context ?? "all");

            // Step 1: Generate embedding for the query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Step 2: Search for similar code in Qdrant
            var results = await _vectorService.SearchSimilarCodeAsync(
                queryEmbedding,
                context: context,
                limit: limit,
                minimumScore: minimumScore,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Found {Count} results for query", results.Count);

            return new QueryResult
            {
                Results = results,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying: {Query}", query);
            throw;
        }
    }
}

