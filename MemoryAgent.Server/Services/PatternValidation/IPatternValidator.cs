using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Interface for pattern-specific validators
/// Each pattern type should have its own validator implementation
/// </summary>
public interface IPatternValidator
{
    /// <summary>
    /// Pattern types this validator handles
    /// </summary>
    IEnumerable<PatternType> SupportedPatternTypes { get; }

    /// <summary>
    /// Validates a specific pattern
    /// </summary>
    PatternQualityResult Validate(CodePattern pattern);

    /// <summary>
    /// Generates migration path for deprecated patterns (if applicable)
    /// </summary>
    PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true);
}

