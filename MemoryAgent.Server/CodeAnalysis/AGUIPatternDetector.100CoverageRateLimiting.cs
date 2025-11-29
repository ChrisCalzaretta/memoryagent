using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Rate Limiting
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Rate Limiting

    private List<CodePattern> DetectRateLimiting(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Rate limiting middleware
        if ((sourceCode.Contains("RateLimit") || sourceCode.Contains("Throttle") || sourceCode.Contains("RateLimiter")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent") || sourceCode.Contains("api")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_RateLimiting",
                type: PatternType.AGUI,
                category: PatternCategory.Performance,
                implementation: "Rate limiting for AG-UI endpoints",
                filePath: filePath,
                lineNumber: 1,
                content: "// Rate limiting detected",
                bestPractice: "Implement rate limiting on AG-UI endpoints to prevent abuse and ensure fair resource allocation.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Rate Limiting",
                    ["algorithms"] = new[] { "Fixed window", "Sliding window", "Token bucket" },
                    ["scope"] = new[] { "Per user", "Per IP", "Per endpoint" }
                }
            ));
        }

        // Pattern: Concurrent connection limits
        if ((sourceCode.Contains("MaxConcurrentConnections") || sourceCode.Contains("ConcurrencyLimiter")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("SSE") || sourceCode.Contains("WebSocket")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ConcurrencyLimit",
                type: PatternType.AGUI,
                category: PatternCategory.Performance,
                implementation: "Concurrent connection limits for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Concurrency limiting detected",
                bestPractice: "Limit concurrent AG-UI connections per user to prevent resource exhaustion and ensure scalability.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit",
                context: context,
                confidence: 0.87f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Concurrency Limit",
                    ["benefits"] = new[] { "Resource protection", "Fair usage", "Scalability" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
