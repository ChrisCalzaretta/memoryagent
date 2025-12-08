using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for managing an evolving pattern catalog that learns and improves.
/// Replaces static BestPracticesCatalog with a learnable, versioned system.
/// </summary>
public interface IEvolvingPatternCatalogService
{
    #region Pattern Retrieval
    
    /// <summary>
    /// Get all active patterns
    /// </summary>
    Task<List<EvolvingPattern>> GetActivePatternsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns by type
    /// </summary>
    Task<List<EvolvingPattern>> GetPatternsByTypeAsync(PatternType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns by category
    /// </summary>
    Task<List<EvolvingPattern>> GetPatternsByCategoryAsync(PatternCategory category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific pattern by name
    /// </summary>
    Task<EvolvingPattern?> GetPatternAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get pattern version history
    /// </summary>
    Task<List<EvolvingPattern>> GetPatternHistoryAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search patterns by keyword
    /// </summary>
    Task<List<EvolvingPattern>> SearchPatternsAsync(string query, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Pattern Management
    
    /// <summary>
    /// Create a new pattern (version 1)
    /// </summary>
    Task<EvolvingPattern> CreatePatternAsync(
        string name,
        PatternType type,
        PatternCategory category,
        string recommendation,
        string referenceUrl,
        List<PatternDetectionRule>? detectionRules = null,
        List<string>? examples = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new version of an existing pattern
    /// </summary>
    Task<EvolvingPattern> CreateVersionAsync(
        string name,
        string evolutionReason,
        string? newRecommendation = null,
        List<PatternDetectionRule>? newDetectionRules = null,
        List<string>? newExamples = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deprecate a pattern
    /// </summary>
    Task DeprecatePatternAsync(string name, string reason, string? supersededBy = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activate a specific pattern version
    /// </summary>
    Task ActivateVersionAsync(string name, int version, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback to previous version
    /// </summary>
    Task RollbackAsync(string name, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Feedback & Learning
    
    /// <summary>
    /// Record that a pattern was detected
    /// </summary>
    Task RecordDetectionAsync(
        string patternName,
        string filePath,
        int lineNumber,
        string codeSnippet,
        float confidence,
        string? sessionId = null,
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record feedback on a pattern detection
    /// </summary>
    Task RecordFeedbackAsync(
        string feedbackId,
        PatternFeedbackType feedbackType,
        string? comments = null,
        string? suggestedImprovement = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a pattern detection was useful
    /// </summary>
    Task RecordUsefulAsync(string patternName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a pattern detection was not useful
    /// </summary>
    Task RecordNotUsefulAsync(string patternName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Suggest a new pattern based on code example
    /// </summary>
    Task<PatternSuggestion> SuggestPatternAsync(
        PatternSuggestionRequest request,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Analytics
    
    /// <summary>
    /// Get metrics for all patterns
    /// </summary>
    Task<PatternCatalogMetrics> GetCatalogMetricsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get metrics for a specific pattern
    /// </summary>
    Task<PatternMetrics> GetPatternMetricsAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get most useful patterns (by usefulness score)
    /// </summary>
    Task<List<EvolvingPattern>> GetMostUsefulPatternsAsync(int limit = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns needing improvement (low usefulness)
    /// </summary>
    Task<List<EvolvingPattern>> GetPatternsNeedingImprovementAsync(float threshold = 0.5f, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recently evolved patterns
    /// </summary>
    Task<List<EvolvingPattern>> GetRecentlyEvolvedAsync(int days = 30, int limit = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns by type or category - useful for code generation prompts
    /// </summary>
    Task<List<EvolvingPattern>> GetPatternsByTypeAsync(string type, int limit = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search patterns by keyword - matches name, description, or recommendation
    /// </summary>
    Task<List<EvolvingPattern>> SearchPatternsAsync(string keyword, int limit = 10, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Initialize patterns from static BestPracticesCatalog (migration)
    /// </summary>
    Task InitializeFromStaticCatalogAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if catalog has been initialized
    /// </summary>
    Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default);
    
    #endregion
}

/// <summary>
/// Metrics for the entire pattern catalog
/// </summary>
public class PatternCatalogMetrics
{
    public int TotalPatterns { get; set; }
    public int ActivePatterns { get; set; }
    public int DeprecatedPatterns { get; set; }
    public int TotalVersions { get; set; }
    public float AvgUsefulnessScore { get; set; }
    public int TotalDetections { get; set; }
    public int DetectionsLast7d { get; set; }
    public int PatternsNeedingImprovement { get; set; }
    public int PatternsEvolvedLast30d { get; set; }
    public Dictionary<PatternType, int> PatternsByType { get; set; } = new();
    public Dictionary<PatternCategory, int> PatternsByCategory { get; set; } = new();
}

/// <summary>
/// Metrics for a specific pattern
/// </summary>
public class PatternMetrics
{
    public string PatternName { get; set; } = string.Empty;
    public int ActiveVersion { get; set; }
    public int TotalVersions { get; set; }
    public int TimesDetected { get; set; }
    public int TimesUseful { get; set; }
    public int TimesNotUseful { get; set; }
    public float UsefulnessScore { get; set; }
    public float Confidence { get; set; }
    public DateTime? LastDetectedAt { get; set; }
    public DateTime? LastEvolvedAt { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}

/// <summary>
/// Result of pattern suggestion
/// </summary>
public class PatternSuggestion
{
    public string SuggestedName { get; set; } = string.Empty;
    public PatternType SuggestedType { get; set; }
    public PatternCategory SuggestedCategory { get; set; }
    public string GeneratedRecommendation { get; set; } = string.Empty;
    public List<PatternDetectionRule> GeneratedRules { get; set; } = new();
    public float Confidence { get; set; }
    public bool AlreadyExists { get; set; }
    public string? ExistingPatternName { get; set; }
}

