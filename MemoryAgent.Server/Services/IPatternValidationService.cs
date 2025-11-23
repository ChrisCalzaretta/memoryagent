using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for validating pattern implementation quality
/// </summary>
public interface IPatternValidationService
{
    /// <summary>
    /// Validate a specific pattern's implementation quality
    /// </summary>
    Task<PatternQualityResult> ValidatePatternQualityAsync(
        string patternId, 
        string? context = null, 
        bool includeAutoFix = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all anti-patterns and poorly implemented patterns
    /// </summary>
    Task<FindAntiPatternsResponse> FindAntiPatternsAsync(
        string context,
        IssueSeverity minSeverity = IssueSeverity.Medium,
        bool includeLegacy = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate security of patterns in a project
    /// </summary>
    Task<ValidateSecurityResponse> ValidateSecurityAsync(
        string context,
        List<PatternType>? patternTypes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get migration path for a legacy pattern
    /// </summary>
    Task<PatternMigrationPath?> GetMigrationPathAsync(
        string patternId,
        bool includeCodeExample = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate all patterns in a project and generate comprehensive report
    /// </summary>
    Task<ProjectValidationReport> ValidateProjectAsync(
        string context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprehensive project validation report
/// </summary>
public class ProjectValidationReport
{
    /// <summary>
    /// Project context
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Overall quality score (0-10)
    /// </summary>
    public int OverallScore { get; set; }

    /// <summary>
    /// Overall security score (0-10)
    /// </summary>
    public int SecurityScore { get; set; }

    /// <summary>
    /// Total patterns detected
    /// </summary>
    public int TotalPatterns { get; set; }

    /// <summary>
    /// Patterns by grade
    /// </summary>
    public Dictionary<string, int> PatternsByGrade { get; set; } = new();

    /// <summary>
    /// Critical issues found
    /// </summary>
    public List<ValidationIssue> CriticalIssues { get; set; } = new();

    /// <summary>
    /// Security vulnerabilities
    /// </summary>
    public List<SecurityVulnerability> SecurityVulnerabilities { get; set; } = new();

    /// <summary>
    /// Legacy patterns that need migration
    /// </summary>
    public List<PatternMigrationPath> LegacyPatterns { get; set; } = new();

    /// <summary>
    /// Top recommendations
    /// </summary>
    public List<string> TopRecommendations { get; set; } = new();

    /// <summary>
    /// Detailed validation results
    /// </summary>
    public List<PatternQualityResult> DetailedResults { get; set; } = new();

    /// <summary>
    /// Summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Generated at
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

