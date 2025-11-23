using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for indexing detected code patterns into Qdrant and Neo4j
/// </summary>
public interface IPatternIndexingService
{
    /// <summary>
    /// Index a list of detected patterns
    /// </summary>
    Task<int> IndexPatternsAsync(List<CodePattern> patterns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index a single pattern
    /// </summary>
    Task IndexPatternAsync(CodePattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pattern count for a context
    /// </summary>
    Task<int> GetPatternCountAsync(string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get patterns by type
    /// </summary>
    Task<List<CodePattern>> GetPatternsByTypeAsync(PatternType type, string? context = null, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search patterns semantically
    /// </summary>
    Task<List<CodePattern>> SearchPatternsAsync(string query, string? context = null, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a pattern by its ID
    /// </summary>
    Task<CodePattern?> GetPatternByIdAsync(string patternId, CancellationToken cancellationToken = default);
}

