using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Error Handling
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Error Handling

    private List<CodePattern> DetectErrorHandling(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI specific error handling
        if ((sourceCode.Contains("AGUIException") || sourceCode.Contains("AGUIError")) ||
            (sourceCode.Contains("catch") && sourceCode.Contains("agent") && sourceCode.Contains("error")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ErrorHandling",
                type: PatternType.AGUI,
                category: PatternCategory.Reliability,
                implementation: "AG-UI error handling",
                filePath: filePath,
                lineNumber: 1,
                content: "// AG-UI error handling detected",
                bestPractice: "Implement comprehensive error handling for AG-UI operations with proper logging and user feedback.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Error Handling",
                    ["best_practices"] = new[] { "Log errors", "User feedback", "Graceful degradation" }
                }
            ));
        }

        // Pattern: Retry with exponential backoff
        if ((sourceCode.Contains("retry") || sourceCode.Contains("Retry")) &&
            (sourceCode.Contains("exponential") || sourceCode.Contains("backoff") || sourceCode.Contains("Polly")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ExponentialBackoff",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Exponential backoff for AG-UI retries",
                filePath: filePath,
                lineNumber: 1,
                content: "// Exponential backoff detected",
                bestPractice: "Use exponential backoff for AG-UI connection retries to handle transient failures gracefully.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Exponential Backoff",
                    ["use_case"] = "Transient failure handling"
                }
            ));
        }

        // Pattern: Circuit breaker
        if (sourceCode.Contains("CircuitBreaker") || sourceCode.Contains("circuit-breaker"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CircuitBreaker",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Circuit breaker for AG-UI resilience",
                filePath: filePath,
                lineNumber: 1,
                content: "// Circuit breaker detected",
                bestPractice: "Circuit breaker prevents cascading failures in AG-UI by failing fast when service is unhealthy.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Circuit Breaker",
                    ["benefits"] = new[] { "Fail fast", "Prevent cascading failures", "Auto-recovery" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
