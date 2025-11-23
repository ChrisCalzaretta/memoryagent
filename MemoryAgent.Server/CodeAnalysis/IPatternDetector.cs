using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Interface for detecting coding patterns in source code
/// </summary>
public interface IPatternDetector
{
    /// <summary>
    /// Detect patterns in source code
    /// </summary>
    List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null);

    /// <summary>
    /// Get supported pattern types for this detector
    /// </summary>
    List<PatternType> GetSupportedPatternTypes();

    /// <summary>
    /// Get supported programming language
    /// </summary>
    string GetLanguage();
}

