using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Caching Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Caching Patterns

    private List<CodePattern> DetectCachingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: MemoryCache usage
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("_cache.Contains") || lines[i].Contains("_cache.Get"))
            {
                var pattern = CreatePattern(
                    name: "MemoryCache_Access",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "MemoryCache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "MemoryCache for in-memory caching",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "memory";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: HttpRuntime.Cache (legacy ASP.NET)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("HttpRuntime.Cache"))
            {
                var pattern = CreatePattern(
                    name: "HttpRuntime_Cache",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "HttpRuntime.Cache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Legacy ASP.NET caching (consider IMemoryCache)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Confidence = 0.8f;
                pattern.Metadata["legacy"] = true;
                pattern.Metadata["suggestion"] = "Upgrade to IMemoryCache for modern .NET";
                patterns.Add(pattern);
            }
        }

        // Pattern 3: OutputCache attribute
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("<OutputCache("))
            {
                var pattern = CreatePattern(
                    name: "OutputCache_Attribute",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "OutputCache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Output caching for web pages",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "output";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    
    #endregion
}
