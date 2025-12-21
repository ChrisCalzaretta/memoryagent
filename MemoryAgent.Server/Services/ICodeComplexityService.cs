using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for analyzing code complexity metrics
/// </summary>
public interface ICodeComplexityService
{
    /// <summary>
    /// Analyze code complexity for a specific file
    /// </summary>
    Task<CodeComplexityResult> AnalyzeFileAsync(string filePath, string? methodName = null, CancellationToken cancellationToken = default);
}























