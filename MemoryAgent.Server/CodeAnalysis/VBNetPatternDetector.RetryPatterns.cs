using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Retry Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Retry Patterns

    private List<CodePattern> DetectRetryPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: For loop with retry logic
        for (int i = 0; i < lines.Length; i++)
        {
            if ((lines[i].Contains("For") && (lines[i].Contains("retry") || lines[i].Contains("attempt"))))
            {
                // Look for Try-Catch in next lines
                var hasTryCatch = false;
                for (int j = i; j < Math.Min(i + 15, lines.Length); j++)
                {
                    if (lines[j].Contains("Try"))
                    {
                        hasTryCatch = true;
                        break;
                    }
                }

                if (hasTryCatch)
                {
                    var pattern = CreatePattern(
                        name: "Manual_RetryLoop",
                        type: PatternType.Resilience,
                        category: PatternCategory.Reliability,
                        implementation: "ManualRetry",
                        filePath: filePath,
                        lineNumber: i + 1,
                        content: GetContext(lines, i, 10),
                        bestPractice: "Manual retry loop (consider using Polly)",
                        azureUrl: AzureRetryUrl,
                        context: context
                    );
                    
                    pattern.Confidence = 0.7f;
                    pattern.Metadata["suggestion"] = "Consider using Polly for robust retry logic";
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    
    #endregion
}
