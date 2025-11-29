using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Logging Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Logging Patterns

    private List<CodePattern> DetectLoggingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: ILogger usage
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("_logger.Log") || lines[i].Contains("_logger.Info") ||
                lines[i].Contains("_logger.Warning") || lines[i].Contains("_logger.Error"))
            {
                var pattern = CreatePattern(
                    name: "Logger_Call",
                    type: PatternType.Logging,
                    category: PatternCategory.Operational,
                    implementation: "ILogger",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Logging with ILogger",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    
    #endregion
}
