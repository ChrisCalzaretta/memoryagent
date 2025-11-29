using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Dependency Injection Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Dependency Injection Patterns

    private List<CodePattern> DetectDependencyInjectionPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Constructor with service parameters
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Public Sub New(") && lines[i].Contains("As I"))
            {
                var pattern = CreatePattern(
                    name: "Constructor_Injection",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "ConstructorInjection",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Constructor dependency injection",
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
