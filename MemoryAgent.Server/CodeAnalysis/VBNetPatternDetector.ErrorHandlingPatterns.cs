using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Error Handling Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Error Handling Patterns

    private List<CodePattern> DetectErrorHandlingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Try-Catch blocks
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals("Try", StringComparison.OrdinalIgnoreCase))
            {
                // Look for logging in Catch block
                var logsException = false;
                for (int j = i; j < Math.Min(i + 30, lines.Length); j++)
                {
                    if (lines[j].Contains("_logger.") || lines[j].Contains("Log."))
                    {
                        logsException = true;
                        break;
                    }
                    if (lines[j].Trim().Equals("End Try", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                var pattern = CreatePattern(
                    name: "TryCatch_Block",
                    type: PatternType.ErrorHandling,
                    category: PatternCategory.Reliability,
                    implementation: "TryCatch",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 15),
                    bestPractice: logsException ? "Exception handling with logging" : "Exception handling (add logging)",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["logs_exception"] = logsException;
                pattern.Confidence = logsException ? 1.0f : 0.6f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    
    #endregion
}
