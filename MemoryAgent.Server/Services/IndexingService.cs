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
    private readonly ISemgrepService _semgrepService;
    private readonly ILogger<IndexingService> _logger;

    public IndexingService(
        ICodeParser codeParser,
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        IGraphService graphService,
        IPathTranslationService pathTranslation,
        ISemgrepService semgrepService,
        ILogger<IndexingService> logger)
    {
        _codeParser = codeParser;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _graphService = graphService;
        _pathTranslation = pathTranslation;
        _semgrepService = semgrepService;
        _logger = logger;
    }

    public async Task<IndexResult> IndexFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IndexResult();
        
        // IMPORTANT: Normalize context to lowercase for consistent storage/search across Neo4j and Qdrant
        var normalizedContext = context?.ToLowerInvariant();

        try
        {
            // Translate path from host to container if needed
            var containerPath = _pathTranslation.TranslateToContainerPath(filePath);
            _logger.LogInformation("Indexing file: {FilePath} (translated to: {ContainerPath}) in context: {Context}", filePath, containerPath, normalizedContext);

            // Step 0: Ensure Qdrant collections exist for this context
            await _vectorService.InitializeCollectionsForContextAsync(normalizedContext, cancellationToken);
            
            // Step 1: Delete existing data for this file (if any) to avoid duplicates
            _logger.LogInformation("Checking for existing data for file: {FilePath}", containerPath);
            await Task.WhenAll(
                _vectorService.DeleteByFilePathAsync(containerPath, normalizedContext, cancellationToken),
                _graphService.DeleteByFilePathAsync(containerPath, cancellationToken)
            );

            // Step 1: Parse the file
            var parseResult = await _codeParser.ParseFileAsync(containerPath, normalizedContext, cancellationToken);
            if (!parseResult.Success)
            {
                result.Errors.AddRange(parseResult.Errors);
                return result;
            }

            // Step 1.5: Extract and index detected patterns
            var detectedPatterns = new List<CodePattern>();
            if (parseResult.CodeElements.Any() && 
                parseResult.CodeElements.First().Metadata.TryGetValue("detected_patterns", out var patternsObj) &&
                patternsObj is List<CodePattern> patterns)
            {
                detectedPatterns = patterns;
                _logger.LogDebug("Found {Count} detected patterns in {FilePath}", detectedPatterns.Count, containerPath);
                
                // Convert patterns to CodeMemory objects
                foreach (var pattern in detectedPatterns)
                {
                    var patternMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = pattern.Name,
                        Content = pattern.Content,
                        FilePath = containerPath,
                        LineNumber = pattern.LineNumber,
                        Context = normalizedContext ?? "default",
                        Metadata = new Dictionary<string, object>
                        {
                            ["pattern_type"] = pattern.Type.ToString(),
                            ["pattern_category"] = pattern.Category.ToString(),
                            ["implementation"] = pattern.Implementation,
                            ["best_practice"] = pattern.BestPractice,
                            ["azure_url"] = pattern.AzureBestPracticeUrl,
                            ["confidence"] = pattern.Confidence,
                            ["language"] = pattern.Language,
                            ["is_positive_pattern"] = pattern.IsPositivePattern
                        }
                    };
                    parseResult.CodeElements.Add(patternMemory);
                }
            }

            // Step 2: Generate embeddings for all code elements
            // Use GetEmbeddingText() for smart, semantic-rich embeddings with metadata prefix
            var embeddings = new List<float[]>();
            var embeddingSuccess = false;
            
            try
            {
                var textsToEmbed = parseResult.CodeElements.Select(e => e.GetEmbeddingText()).ToList();
                embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);

                // Assign embeddings to code elements
                for (int i = 0; i < parseResult.CodeElements.Count; i++)
                {
                    parseResult.CodeElements[i].Embedding = embeddings[i];
                }
                
                embeddingSuccess = true;
                _logger.LogDebug("Successfully generated {Count} embeddings for {File}", embeddings.Count, containerPath);
            }
            catch (Exception embeddingEx)
            {
                _logger.LogWarning(embeddingEx, 
                    "Embedding generation failed for {File}. Continuing with Neo4j-only storage (graph relationships will work, semantic search will not).",
                    containerPath);
                
                // Note: Using Errors list to track this, though it's not a fatal error
                result.Errors.Add("Warning: Semantic search unavailable - embedding generation failed");
            }

            // Step 3: Store in parallel (Qdrant + Neo4j)
            // Only store in Qdrant if embeddings succeeded
            Task? storeVectorTask = null;
            if (embeddingSuccess && embeddings.Any())
            {
                storeVectorTask = _vectorService.StoreCodeMemoriesAsync(parseResult.CodeElements, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Skipping Qdrant storage for {File} (no embeddings)", containerPath);
            }
            
            // Always store in Neo4j (graph structure, relationships, patterns)
            var storeGraphTask = _graphService.StoreCodeNodesAsync(parseResult.CodeElements, cancellationToken);

            // Wait for storage tasks
            if (storeVectorTask != null)
            {
                await Task.WhenAll(storeVectorTask, storeGraphTask);
            }
            else
            {
                await storeGraphTask;
            }

            // Step 4: Create relationships in Neo4j
            if (parseResult.Relationships.Any())
            {
                await _graphService.CreateRelationshipsAsync(parseResult.Relationships, cancellationToken);
            }

            // Step 5: Store pattern nodes in Neo4j
            if (detectedPatterns.Any())
            {
                foreach (var pattern in detectedPatterns)
                {
                    await _graphService.StorePatternNodeAsync(pattern, cancellationToken);
                }
                _logger.LogDebug("Stored {Count} pattern nodes in Neo4j for {FilePath}", detectedPatterns.Count, containerPath);
            }

            // Step 6: Run Semgrep security scan
            var securityIssuesFound = 0;
            try
            {
                var semgrepReport = await _semgrepService.ScanFileAsync(containerPath, cancellationToken);
                
                if (semgrepReport.Success && semgrepReport.Findings.Any())
                {
                    _logger.LogInformation("Semgrep found {Count} security issues in {File}", 
                        semgrepReport.Findings.Count, containerPath);

                    // Store findings as security patterns
                    foreach (var finding in semgrepReport.Findings)
                    {
                        var securityPattern = new CodePattern
                        {
                            Type = PatternType.Security,
                            Category = PatternCategory.Security,
                            Name = $"Semgrep_{finding.RuleId}_{Path.GetFileName(containerPath)}",
                            Implementation = finding.Message,
                            FilePath = containerPath,
                            LineNumber = finding.StartLine,
                            Content = finding.CodeSnippet,
                            IsPositivePattern = false,  // Security findings are issues/anti-patterns
                            Confidence = finding.Severity == "ERROR" ? 1.0f : 0.8f,
                            Context = normalizedContext ?? "default",
                            BestPractice = finding.Metadata.OWASP ?? "OWASP Top 10",
                            AzureBestPracticeUrl = "https://owasp.org/www-project-top-ten/",
                            Metadata = new Dictionary<string, object>
                            {
                                ["semgrep_rule"] = finding.RuleId,
                                ["cwe"] = finding.Metadata.CWE ?? "Unknown",
                                ["owasp"] = finding.Metadata.OWASP ?? "Unknown",
                                ["severity"] = finding.Severity,
                                ["confidence"] = finding.Metadata.Confidence ?? "HIGH",
                                ["fix"] = finding.Fix ?? "See Semgrep rule documentation",
                                ["start_line"] = finding.StartLine,
                                ["end_line"] = finding.EndLine,
                                ["is_semgrep_finding"] = true
                            }
                        };

                        await _graphService.StorePatternNodeAsync(securityPattern, cancellationToken);
                        securityIssuesFound++;
                    }

                    _logger.LogDebug("Stored {Count} Semgrep findings as security patterns", securityIssuesFound);
                }
                else if (!semgrepReport.Success)
                {
                    _logger.LogWarning("Semgrep scan failed for {File}: {Errors}", 
                        containerPath, string.Join(", ", semgrepReport.Errors));
                }
            }
            catch (Exception semgrepEx)
            {
                _logger.LogWarning(semgrepEx, "Semgrep scan error for {File} - continuing without security scan", containerPath);
                // Don't fail entire indexing if Semgrep fails
            }

            // Update result
            result.Success = true;
            result.FilesIndexed = 1;
            result.ClassesFound = parseResult.CodeElements.Count(e => e.Type == CodeMemoryType.Class);
            result.MethodsFound = parseResult.CodeElements.Count(e => e.Type == CodeMemoryType.Method);
            result.PatternsDetected = parseResult.CodeElements.Count(e => e.Type == CodeMemoryType.Pattern) + securityIssuesFound;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Successfully indexed {FilePath}: {Classes} classes, {Methods} methods, {Patterns} patterns in {Duration}ms",
                filePath, result.ClassesFound, result.MethodsFound, result.PatternsDetected, stopwatch.ElapsedMilliseconds);
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
        CancellationToken cancellationToken = default,
        Action<string>? progressCallback = null)
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

            // Find all supported code files
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var patterns = new[] 
            { 
                "*.cs", "*.vb", "*.cshtml", "*.razor", "*.py", "*.md", 
                "*.css", "*.scss", "*.less", 
                "*.js", "*.jsx", "*.ts", "*.tsx",
                "*.dart",  // Flutter/Dart support
                "*.tf", "*.tfvars",  // Terraform/Infrastructure as Code support
                "*.csproj", "*.vbproj", "*.fsproj", "*.sln",
                "*.json", "*.yml", "*.yaml", "*.config",
                "*.bicep",
                "Dockerfile", "*.dockerfile"
            };
            
            var codeFiles = patterns
                .SelectMany(pattern => Directory.GetFiles(containerPath, pattern, searchOption))
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.Contains("/obj/") && !f.Contains("/bin/") 
                         && !f.Contains("\\node_modules\\") && !f.Contains("/node_modules/"))
                // Exclude .cshtml.cs and .razor.cs files (they'll be picked up by *.cs pattern)
                .Where(f => !f.EndsWith(".cshtml.cs") && !f.EndsWith(".razor.cs"))
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} code files to index ({CSharp} .cs, {VB} .vb, {Razor} .cshtml/.razor, {Python} .py, {Dart} .dart, {Markdown} .md, {Styles} .css/.scss/.less, {JavaScript} .js/.ts, {Project} .csproj/.sln, {Config} config files, {Docker} Docker)", 
                codeFiles.Count,
                codeFiles.Count(f => f.EndsWith(".cs")),
                codeFiles.Count(f => f.EndsWith(".vb")),
                codeFiles.Count(f => f.EndsWith(".cshtml") || f.EndsWith(".razor")),
                codeFiles.Count(f => f.EndsWith(".py")),
                codeFiles.Count(f => f.EndsWith(".dart")),
                codeFiles.Count(f => f.EndsWith(".md")),
                codeFiles.Count(f => f.EndsWith(".css") || f.EndsWith(".scss") || f.EndsWith(".less")),
                codeFiles.Count(f => f.EndsWith(".js") || f.EndsWith(".ts") || f.EndsWith(".jsx") || f.EndsWith(".tsx")),
                codeFiles.Count(f => f.EndsWith(".csproj") || f.EndsWith(".sln") || f.EndsWith(".vbproj") || f.EndsWith(".fsproj")),
                codeFiles.Count(f => f.EndsWith(".json") || f.EndsWith(".config") || f.EndsWith(".yml") || f.EndsWith(".yaml")),
                codeFiles.Count(f => f.Contains("Dockerfile") || f.Contains("docker-compose")));

            // Index files in parallel (but limit concurrency to avoid overwhelming services)
            var semaphore = new SemaphoreSlim(8); // Max 8 concurrent file indexes
            var indexTasks = codeFiles.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var fileResult = await IndexFileAsync(file, context, cancellationToken);
                    // Report progress after each file
                    progressCallback?.Invoke(file);
                    return fileResult;
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
                result.PatternsDetected += fileResult.PatternsDetected;
                result.Errors.AddRange(fileResult.Errors);
            }

            result.Success = !result.Errors.Any();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Directory indexing completed: {Files} files, {Classes} classes, {Methods} methods, {Patterns} patterns in {Duration}s",
                result.FilesIndexed, result.ClassesFound, result.MethodsFound, result.PatternsDetected, stopwatch.Elapsed.TotalSeconds);
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

