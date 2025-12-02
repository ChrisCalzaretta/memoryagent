using System.Diagnostics;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Smart search service with auto-detection of search strategy
/// </summary>
public class SmartSearchService : ISmartSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    private readonly IPatternIndexingService _patternService;
    private readonly ILogger<SmartSearchService> _logger;

    // Graph query patterns
    private static readonly string[] GraphPatterns = new[]
    {
        "implement", "implements", "implementation",
        "inherit", "inherits", "inheritance", "extends",
        "interface",
        "call", "calls", "calling",
        "use", "uses", "using",
        "depend", "depends", "dependency",
        "relationship",
        "that have", "that has",
        "with attribute", "with annotation"
    };

    // Pattern query keywords
    private static readonly string[] PatternKeywords = new[]
    {
        "pattern", "patterns",
        "caching", "cache",
        "retry", "retries",
        "validation", "validate",
        "authentication", "auth",
        "authorization",
        "logging", "logs",
        "monitoring", "health check",
        "background job", "background task",
        "circuit breaker",
        "rate limit", "throttling",
        "pagination",
        "versioning",
        "encryption",
        "best practice", "best practices"
    };

    public SmartSearchService(
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        IGraphService graphService,
        IPatternIndexingService patternService,
        ILogger<SmartSearchService> logger)
    {
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _graphService = graphService;
        _patternService = patternService;
        _logger = logger;
    }

    public async Task<SmartSearchResponse> SearchAsync(SmartSearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Classify the query
            var strategy = ClassifyQuery(request.Query);
            _logger.LogInformation("Smart search query: '{Query}' classified as: {Strategy}", request.Query, strategy);

            var response = new SmartSearchResponse
            {
                Query = request.Query,
                Strategy = strategy
            };

            // Execute search based on strategy
            List<SmartSearchResult> results = strategy switch
            {
                "pattern-search" => await ExecutePatternSearchAsync(request, cancellationToken),
                "graph-first" => await ExecuteGraphFirstSearchAsync(request, cancellationToken),
                "semantic-first" => await ExecuteSemanticFirstSearchAsync(request, cancellationToken),
                "hybrid" => await ExecuteHybridSearchAsync(request, cancellationToken),
                _ => await ExecuteSemanticFirstSearchAsync(request, cancellationToken)
            };

            // Apply pagination
            response.TotalFound = results.Count;
            response.Results = results
                .Skip(request.Offset)
                .Take(request.Limit)
                .ToList();
            response.HasMore = (request.Offset + request.Limit) < results.Count;

            // Add metadata
            response.ProcessingTime = stopwatch.Elapsed;
            response.Metadata["totalResults"] = results.Count;
            response.Metadata["returnedResults"] = response.Results.Count;
            response.Metadata["offset"] = request.Offset;
            response.Metadata["limit"] = request.Limit;

            _logger.LogInformation(
                "Smart search completed: {Results} results in {Duration}ms using {Strategy}",
                response.Results.Count, stopwatch.ElapsedMilliseconds, strategy);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing smart search for query: {Query}", request.Query);
            throw;
        }
    }

    private string ClassifyQuery(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // Check for pattern queries first (highest priority)
        var patternScore = PatternKeywords.Count(keyword => lowerQuery.Contains(keyword));
        if (patternScore >= 1)
        {
            _logger.LogDebug("Detected pattern query: {PatternScore} pattern keywords found", patternScore);
            return "pattern-search";
        }

        // Check for graph patterns
        var graphScore = GraphPatterns.Count(pattern => lowerQuery.Contains(pattern));

        // Check for specific class/method names (PascalCase or camelCase)
        var hasSpecificNames = Regex.IsMatch(query, @"\b[A-Z][a-zA-Z0-9]+(?:\.[A-Z][a-zA-Z0-9]+)*\b");

        // Graph-first if:
        // - Contains graph keywords
        // - References specific classes/interfaces
        // - Asks about relationships
        if (graphScore >= 2 || (graphScore >= 1 && hasSpecificNames))
        {
            return "graph-first";
        }

        // Hybrid if has some graph indicators but also conceptual
        if (graphScore == 1)
        {
            return "hybrid";
        }

        // Default to semantic for natural language / conceptual queries
        return "semantic-first";
    }

    private async Task<List<SmartSearchResult>> ExecuteGraphFirstSearchAsync(
        SmartSearchRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<SmartSearchResult>();

        try
        {
            // Step 1: Query Neo4j for matching patterns
            var graphResults = await QueryGraphAsync(request.Query, request.Context, cancellationToken);

            _logger.LogInformation("Graph query returned {Count} results", graphResults.Count);

            // Step 2: Enrich with semantic data from Qdrant
            foreach (var graphResult in graphResults.Take(request.Limit * 2)) // Get more from graph, will filter after enrichment
            {
                try
                {
                    // Get embedding for this element
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(graphResult.Name, cancellationToken);
                    
                    // Search Qdrant for similar code
                    var semanticMatches = await _vectorService.SearchSimilarCodeAsync(
                        embedding,
                        context: request.Context,
                        limit: 1,
                        minimumScore: 0.1f, // Low threshold, we already filtered by graph
                        cancellationToken: cancellationToken);

                    var semanticMatch = semanticMatches.FirstOrDefault();
                    var semanticScore = semanticMatch?.Score ?? 0.5f;

                    // Create enriched result
                    var result = new SmartSearchResult
                    {
                        Name = graphResult.Name,
                        Type = graphResult.Type,
                        Content = semanticMatch?.Code ?? graphResult.Content,
                        FilePath = graphResult.FilePath,
                        LineNumber = semanticMatch?.LineNumber ?? 0,
                        GraphScore = graphResult.Score,
                        SemanticScore = semanticScore,
                        Score = (graphResult.Score * 0.7f) + (semanticScore * 0.3f), // Graph-weighted
                        Metadata = graphResult.Metadata
                    };

                    // Add relationships if requested
                    if (request.IncludeRelationships)
                    {
                        result.Relationships = await GetRelationshipsAsync(
                            graphResult.Name,
                            request.Context,
                            request.RelationshipDepth,
                            cancellationToken);
                    }

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error enriching graph result: {Name}", graphResult.Name);
                }
            }

            // Sort by combined score
            results = results
                .OrderByDescending(r => r.Score)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in graph-first search");
        }

        return results;
    }

    private async Task<List<SmartSearchResult>> ExecuteSemanticFirstSearchAsync(
        SmartSearchRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<SmartSearchResult>();

        try
        {
            // Step 1: Generate embedding for query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);

            // Step 2: Search Qdrant
            var semanticResults = await _vectorService.SearchSimilarCodeAsync(
                queryEmbedding,
                context: request.Context,
                limit: request.Limit * 2, // Get more, will enrich and filter
                minimumScore: request.MinimumScore,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Semantic search returned {Count} results", semanticResults.Count);

            // Step 3: Enrich with graph relationships
            foreach (var semanticResult in semanticResults)
            {
                try
                {
                    var result = new SmartSearchResult
                    {
                        Name = semanticResult.Name,
                        Type = semanticResult.Type.ToString(),
                        Content = semanticResult.Code,
                        FilePath = semanticResult.FilePath,
                        LineNumber = semanticResult.LineNumber,
                        SemanticScore = semanticResult.Score,
                        GraphScore = 0.5f, // Will be updated if graph data found
                        Score = semanticResult.Score, // Start with semantic score
                        Metadata = semanticResult.Metadata
                    };

                    // Add relationships if requested
                    if (request.IncludeRelationships)
                    {
                        result.Relationships = await GetRelationshipsAsync(
                            semanticResult.Name,
                            request.Context,
                            request.RelationshipDepth,
                            cancellationToken);

                        // Boost score if has relationships
                        if (result.Relationships != null && result.Relationships.Any())
                        {
                            var relationshipBoost = Math.Min(0.15f, result.Relationships.Sum(r => r.Value.Count) * 0.02f);
                            result.GraphScore = 0.5f + relationshipBoost;
                            result.Score = (result.SemanticScore * 0.7f) + (result.GraphScore * 0.3f);
                        }
                    }

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error enriching semantic result: {Name}", semanticResult.Name);
                }
            }

            // Sort by combined score
            results = results
                .OrderByDescending(r => r.Score)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in semantic-first search");
        }

        return results;
    }

    private async Task<List<SmartSearchResult>> ExecuteHybridSearchAsync(
        SmartSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing hybrid search (parallel graph + semantic)");

            // Execute both strategies in parallel
            var graphTask = ExecuteGraphFirstSearchAsync(request, cancellationToken);
            var semanticTask = ExecuteSemanticFirstSearchAsync(request, cancellationToken);

            await Task.WhenAll(graphTask, semanticTask);

            var graphResults = await graphTask;
            var semanticResults = await semanticTask;

            // Merge and deduplicate results
            var mergedResults = new Dictionary<string, SmartSearchResult>();

            // Add graph results
            foreach (var result in graphResults)
            {
                var key = $"{result.FilePath}:{result.Name}";
                mergedResults[key] = result;
            }

            // Merge semantic results
            foreach (var result in semanticResults)
            {
                var key = $"{result.FilePath}:{result.Name}";
                if (mergedResults.TryGetValue(key, out var existing))
                {
                    // Average the scores
                    existing.Score = (existing.Score + result.Score) / 2;
                    existing.SemanticScore = Math.Max(existing.SemanticScore, result.SemanticScore);
                    existing.GraphScore = Math.Max(existing.GraphScore, result.GraphScore);
                }
                else
                {
                    mergedResults[key] = result;
                }
            }

            // Sort by combined score
            return mergedResults.Values
                .OrderByDescending(r => r.Score)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hybrid search");
            return new List<SmartSearchResult>();
        }
    }

    private async Task<List<GraphQueryResult>> QueryGraphAsync(string query, string? context, CancellationToken cancellationToken)
    {
        // Parse query for graph patterns and build Cypher query
        // This is a simplified version - can be enhanced with more sophisticated parsing

        var results = new List<GraphQueryResult>();

        try
        {
            // Example: Extract interface name from query like "classes that implement IRepository"
            var implementMatch = Regex.Match(query, @"implement(?:s)?\s+([A-Z][a-zA-Z0-9<>]+)", RegexOptions.IgnoreCase);
            if (implementMatch.Success)
            {
                var interfaceName = implementMatch.Groups[1].Value;
                var impactResults = await _graphService.GetImpactAnalysisAsync(interfaceName, cancellationToken);
                
                foreach (var className in impactResults.Take(50))
                {
                    results.Add(new GraphQueryResult
                    {
                        Name = className,
                        Type = "Class",
                        FilePath = "",
                        Content = className,
                        Score = 0.9f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["impactType"] = "implementation"
                        }
                    });
                }
            }

            // If no specific pattern found, do a general graph search
            if (!results.Any())
            {
                _logger.LogInformation("No specific graph pattern found, performing general Neo4j text search");
                
                // Fallback: Do a full-text search across all node types in Neo4j
                var generalResults = await _graphService.FullTextSearchAsync(query, context, 50, cancellationToken);
                
                foreach (var node in generalResults)
                {
                    results.Add(new GraphQueryResult
                    {
                        Name = node.Name,
                        Type = node.Type.ToString(),
                        FilePath = node.FilePath,
                        Content = node.Content,
                        Score = 0.7f,  // Moderate score for general search
                        Metadata = node.Metadata
                    });
                }
                
                _logger.LogInformation("General graph search returned {Count} results", results.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying graph");
        }

        return results;
    }

    private async Task<Dictionary<string, List<string>>> GetRelationshipsAsync(
        string elementName,
        string? context,
        int depth,
        CancellationToken cancellationToken)
    {
        var relationships = new Dictionary<string, List<string>>();

        try
        {
            // Get impact analysis (what uses this element)
            var impactResults = await _graphService.GetImpactAnalysisAsync(elementName, cancellationToken);
            
            if (impactResults.Any())
            {
                relationships["usedBy"] = impactResults.Take(10).ToList();
            }

            // Get dependency chain (what this element uses)
            var dependencyResults = await _graphService.GetDependencyChainAsync(elementName, depth, cancellationToken);
            
            if (dependencyResults.Any())
            {
                relationships["dependencies"] = dependencyResults.Take(10).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting relationships for: {ElementName}", elementName);
        }

        return relationships;
    }

    private async Task<List<SmartSearchResult>> ExecutePatternSearchAsync(
        SmartSearchRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<SmartSearchResult>();

        try
        {
            _logger.LogInformation("Executing pattern search for query: {Query}", request.Query);

            // Search patterns using semantic search
            var patterns = await _patternService.SearchPatternsAsync(
                request.Query,
                request.Context,
                request.Limit * 2, // Get more results for better filtering
                cancellationToken);

            _logger.LogDebug("Found {Count} patterns", patterns.Count);

            // Convert patterns to SmartSearchResult
            foreach (var pattern in patterns)
            {
                var result = new SmartSearchResult
                {
                    Name = pattern.Name,
                    Type = "Pattern",
                    FilePath = pattern.FilePath,
                    LineNumber = pattern.LineNumber,
                    Content = pattern.Content,
                    SemanticScore = pattern.Confidence,
                    GraphScore = pattern.Confidence,
                    Metadata = new Dictionary<string, object>
                    {
                        ["pattern_type"] = pattern.Type.ToString(),
                        ["pattern_category"] = pattern.Category.ToString(),
                        ["implementation"] = pattern.Implementation,
                        ["best_practice"] = pattern.BestPractice,
                        ["azure_url"] = pattern.AzureBestPracticeUrl,
                        ["language"] = pattern.Language,
                        ["is_positive_pattern"] = pattern.IsPositivePattern,
                        ["detected_at"] = pattern.DetectedAt.ToString("O")
                    }
                };

                // Add relationship information if requested
                if (request.IncludeRelationships)
                {
                    result.Relationships = new Dictionary<string, List<string>>
                    {
                        ["file"] = new List<string> { pattern.FilePath }
                    };
                }

                results.Add(result);
            }

            _logger.LogInformation("Pattern search returned {Count} results", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing pattern search");
        }

        return results;
    }

    private class GraphQueryResult
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

