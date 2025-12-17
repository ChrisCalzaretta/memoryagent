using MemoryAgent.PatternManagement.Models;

namespace MemoryAgent.PatternManagement.Services;

/// <summary>
/// Service for managing global patterns
/// </summary>
public interface IPatternService
{
    /// <summary>
    /// Create a new global pattern
    /// </summary>
    Task<GlobalPattern> CreatePatternAsync(GlobalPattern pattern, CancellationToken ct = default);
    
    /// <summary>
    /// Update an existing pattern
    /// </summary>
    Task<GlobalPattern> UpdatePatternAsync(GlobalPattern pattern, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a pattern by ID
    /// </summary>
    Task<bool> DeletePatternAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Get a pattern by ID
    /// </summary>
    Task<GlobalPattern?> GetPatternAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Get a pattern by name
    /// </summary>
    Task<GlobalPattern?> GetPatternByNameAsync(string name, CancellationToken ct = default);
    
    /// <summary>
    /// List all patterns with optional filtering
    /// </summary>
    Task<List<GlobalPattern>> ListPatternsAsync(
        string? category = null, 
        PatternSeverity? severity = null,
        bool includeDeprecated = false,
        CancellationToken ct = default);
    
    /// <summary>
    /// Search patterns by semantic similarity
    /// </summary>
    Task<List<GlobalPattern>> SearchPatternsAsync(string query, int limit = 10, CancellationToken ct = default);
    
    /// <summary>
    /// Get patterns that a given pattern requires
    /// </summary>
    Task<List<GlobalPattern>> GetRequiredPatternsAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Get patterns that conflict with a given pattern
    /// </summary>
    Task<List<GlobalPattern>> GetConflictingPatternsAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Get the pattern that supersedes a deprecated pattern
    /// </summary>
    Task<GlobalPattern?> GetSupersedesPatternAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Import patterns from JSON
    /// </summary>
    Task<int> ImportPatternsAsync(string json, CancellationToken ct = default);
    
    /// <summary>
    /// Export all patterns to JSON
    /// </summary>
    Task<string> ExportPatternsAsync(CancellationToken ct = default);
}







