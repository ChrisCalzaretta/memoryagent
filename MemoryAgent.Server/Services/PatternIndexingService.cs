using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for indexing detected code patterns into Qdrant and Neo4j
/// </summary>
public class PatternIndexingService : IPatternIndexingService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    private readonly ILogger<PatternIndexingService> _logger;

    public PatternIndexingService(
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        IGraphService graphService,
        ILogger<PatternIndexingService> logger)
    {
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<int> IndexPatternsAsync(List<CodePattern> patterns, CancellationToken cancellationToken = default)
    {
        if (!patterns.Any())
        {
            return 0;
        }

        var indexed = 0;

        foreach (var pattern in patterns)
        {
            try
            {
                await IndexPatternAsync(pattern, cancellationToken);
                indexed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to index pattern {PatternName} from {FilePath}", 
                    pattern.Name, pattern.FilePath);
            }
        }

        _logger.LogInformation("Indexed {Count} patterns from {Total} detected", indexed, patterns.Count);
        return indexed;
    }

    public async Task IndexPatternAsync(CodePattern pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the pattern using smart embedding text
            // Note: For patterns, we build custom embedding text with pattern-specific metadata
            // This is similar to CodeMemory.GetEmbeddingText() but optimized for patterns
            var embeddingText = $"[PATTERN] {pattern.Name}\n" +
                               $"Type: {pattern.Type}\n" +
                               $"Category: {pattern.Category}\n" +
                               $"Best Practice: {pattern.BestPractice}\n" +
                               $"Implementation: {pattern.Implementation}\n" +
                               $"Code:\n{pattern.Content}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);

            // Convert to CodeMemory for storage
            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = pattern.Name,
                Content = pattern.Content,
                FilePath = pattern.FilePath,
                LineNumber = pattern.LineNumber,
                Context = pattern.Context,
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = pattern.Type.ToString(),
                    ["pattern_category"] = pattern.Category.ToString(),
                    ["implementation"] = pattern.Implementation,
                    ["best_practice"] = pattern.BestPractice,
                    ["azure_url"] = pattern.AzureBestPracticeUrl,
                    ["confidence"] = pattern.Confidence,
                    ["language"] = pattern.Language,
                    ["is_positive_pattern"] = pattern.IsPositivePattern,
                    ["detected_at"] = pattern.DetectedAt.ToString("O")
                }
            };

            // Add all pattern metadata
            foreach (var kvp in pattern.Metadata)
            {
                codeMemory.Metadata[$"pattern_{kvp.Key}"] = kvp.Value;
            }

            // Store in Qdrant
            codeMemory.Embedding = embedding;
            await _vectorService.StoreCodeMemoryAsync(codeMemory, cancellationToken);

            // Store pattern node in Neo4j
            await _graphService.StorePatternNodeAsync(pattern, cancellationToken);

            _logger.LogDebug("Indexed pattern {PatternName} ({Type}) from {FilePath}:{LineNumber}",
                pattern.Name, pattern.Type, pattern.FilePath, pattern.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing pattern {PatternName}", pattern.Name);
            throw;
        }
    }

    public async Task<int> GetPatternCountAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        
        try
        {
            // Simplified: Use search to estimate count
            // TODO: Implement proper count API when available
            var dummyEmbedding = new float[1024]; // Match model dimension
            var results = await _vectorService.SearchSimilarCodeAsync(
                dummyEmbedding,
                type: CodeMemoryType.Pattern,
                context: normalizedContext,
                limit: 1000,
                minimumScore: 0.0f,
                cancellationToken: cancellationToken);
            
            return results.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pattern count for context: {Context}", normalizedContext);
            return 0;
        }
    }

    public async Task<List<CodePattern>> GetPatternsByTypeAsync(
        PatternType type, 
        string? context = null, 
        int limit = 100, 
        CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        
        try
        {
            // Simplified: Use GraphService which has pattern retrieval
            return await _graphService.GetPatternsByTypeAsync(type, normalizedContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patterns by type: {Type}", type);
            return new List<CodePattern>();
        }
    }

    public async Task<List<CodePattern>> SearchPatternsAsync(
        string query, 
        string? context = null, 
        int limit = 20, 
        CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        
        try
        {
            // Generate embedding for the search query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Search Qdrant for similar patterns
            var results = await _vectorService.SearchSimilarCodeAsync(
                queryEmbedding,
                context: normalizedContext,
                limit: limit,
                minimumScore: 0.5f,
                cancellationToken: cancellationToken);

            // Filter for patterns only
            var patternResults = results.Where(r => r.Type == CodeMemoryType.Pattern).ToList();

            return patternResults.Select(r => new CodePattern
            {
                Id = r.Metadata.GetValueOrDefault("id")?.ToString() ?? Guid.NewGuid().ToString(),
                Name = r.Name,
                Type = Enum.TryParse<PatternType>(r.Metadata.GetValueOrDefault("pattern_type")?.ToString(), out var pt) ? pt : PatternType.Unknown,
                Category = Enum.TryParse<PatternCategory>(r.Metadata.GetValueOrDefault("pattern_category")?.ToString(), out var pc) ? pc : PatternCategory.General,
                Implementation = r.Metadata.GetValueOrDefault("implementation")?.ToString() ?? "",
                Language = r.Metadata.GetValueOrDefault("language")?.ToString() ?? "",
                FilePath = r.FilePath,
                LineNumber = r.LineNumber,
                Content = r.Code,
                BestPractice = r.Metadata.GetValueOrDefault("best_practice")?.ToString() ?? "",
                AzureBestPracticeUrl = r.Metadata.GetValueOrDefault("azure_url")?.ToString() ?? "",
                Confidence = ParseFloatFromMetadata(r.Metadata, "confidence", 1.0f),
                IsPositivePattern = ParseBoolFromMetadata(r.Metadata, "is_positive_pattern", true),
                Context = normalizedContext ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching patterns with query: {Query}", query);
            return new List<CodePattern>();
        }
    }

    public async Task<CodePattern?> GetPatternByIdAsync(string patternId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Pattern IDs are typically in format "PatternType_Name" or a file path
            // Try searching by name first
            var patterns = await SearchPatternsAsync(patternId, limit: 10, cancellationToken: cancellationToken);
            
            // Return exact match by ID or name
            var pattern = patterns.FirstOrDefault(p => p.Id == patternId || p.Name == patternId);
            
            return pattern;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pattern by ID: {PatternId}", patternId);
            return null;
        }
    }

    private float ParseFloatFromMetadata(Dictionary<string, object> metadata, string key, float defaultValue)
    {
        try
        {
            var value = metadata.GetValueOrDefault(key);
            if (value == null) return defaultValue;

            // Handle different types
            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is int i) return i;
            if (value is string s && float.TryParse(s, out var parsed)) return parsed;
            
            // Handle JsonElement
            var str = value.ToString();
            if (float.TryParse(str, out var result)) return result;
            
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private bool ParseBoolFromMetadata(Dictionary<string, object> metadata, string key, bool defaultValue)
    {
        try
        {
            var value = metadata.GetValueOrDefault(key);
            if (value == null) return defaultValue;

            // Handle different types
            if (value is bool b) return b;
            if (value is string s && bool.TryParse(s, out var parsed)) return parsed;
            
            // Handle JsonElement
            var str = value.ToString();
            if (bool.TryParse(str, out var result)) return result;
            
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}

