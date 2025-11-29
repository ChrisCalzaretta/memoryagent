using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Error Handling Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Error Handling Patterns




    private List<CodePattern> DetectErrorHandlingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: try-catch blocks
        var tryCatchBlocks = root.DescendantNodes()
            .OfType<TryStatementSyntax>();

        foreach (var tryCatch in tryCatchBlocks)
        {
            // Check if it logs the exception
            var logsException = tryCatch.Catches.Any(c => 
                c.Block.ToString().Contains("Log") || 
                c.Block.ToString().Contains("_logger"));

            var pattern = CreatePattern(
                name: "TryCatch_Block",
                type: PatternType.ErrorHandling,
                category: PatternCategory.Reliability,
                implementation: "TryCatch",
                filePath: filePath,
                node: tryCatch,
                sourceCode: sourceCode,
                bestPractice: logsException ? "Exception handling with logging" : "Exception handling (add logging)",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["logs_exception"] = logsException;
            pattern.Confidence = logsException ? 1.0f : 0.6f;
            patterns.Add(pattern);
        }

        // Pattern 2: Custom exceptions
        var customExceptions = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("Exception")) == true);

        foreach (var ex in customExceptions)
        {
            var pattern = CreatePattern(
                name: ex.Identifier.Text,
                type: PatternType.ErrorHandling,
                category: PatternCategory.Reliability,
                implementation: "CustomException",
                filePath: filePath,
                node: ex,
                sourceCode: sourceCode,
                bestPractice: "Custom exception for domain-specific errors",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        // Pattern 3: UseExceptionHandler middleware
        var exceptionHandlerCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("UseExceptionHandler"));

        foreach (var handler in exceptionHandlerCalls)
        {
            var pattern = CreatePattern(
                name: "GlobalExceptionHandler",
                type: PatternType.ErrorHandling,
                category: PatternCategory.Reliability,
                implementation: "ExceptionHandlerMiddleware",
                filePath: filePath,
                node: handler,
                sourceCode: sourceCode,
                bestPractice: "Global exception handler middleware",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
