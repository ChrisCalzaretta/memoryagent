using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Logging Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Logging Patterns




    private List<CodePattern> DetectLoggingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: ILogger structured logging
        var loggerCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("_logger.Log") || 
                         inv.ToString().Contains("_logger.LogInformation") ||
                         inv.ToString().Contains("_logger.LogWarning") ||
                         inv.ToString().Contains("_logger.LogError"));

        foreach (var log in loggerCalls)
        {
            // Check if it's structured logging (has placeholders like {UserId})
            var isStructured = log.ToString().Contains("{") && log.ToString().Contains("}");
            
            var pattern = CreatePattern(
                name: isStructured ? "StructuredLogging" : "BasicLogging",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "ILogger",
                filePath: filePath,
                node: log,
                sourceCode: sourceCode,
                bestPractice: isStructured ? "Structured logging with ILogger" : "Basic logging (consider structured logging)",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["structured"] = isStructured;
            pattern.Confidence = isStructured ? 1.0f : 0.7f;
            patterns.Add(pattern);
        }

        // Pattern 2: Log.Information (Serilog)
        var serilogCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().StartsWith("Log."));

        foreach (var log in serilogCalls)
        {
            var pattern = CreatePattern(
                name: "Serilog_Logging",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "Serilog",
                filePath: filePath,
                node: log,
                sourceCode: sourceCode,
                bestPractice: "Serilog structured logging",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Serilog";
            patterns.Add(pattern);
        }

        // Pattern 3: BeginScope
        var scopeCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("BeginScope"));

        foreach (var scope in scopeCalls)
        {
            var pattern = CreatePattern(
                name: "LogScope",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "ILogger.BeginScope",
                filePath: filePath,
                node: scope,
                sourceCode: sourceCode,
                bestPractice: "Log scopes for contextual logging",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["feature"] = "log_scope";
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
