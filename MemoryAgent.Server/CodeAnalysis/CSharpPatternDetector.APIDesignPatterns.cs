using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// API Design Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region API Design Patterns




    private List<CodePattern> DetectApiDesignPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Async/await
        var asyncMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)));

        foreach (var method in asyncMethods)
        {
            var pattern = CreatePattern(
                name: $"{method.Identifier.Text}_Async",
                type: PatternType.ApiDesign,
                category: PatternCategory.Performance,
                implementation: "AsyncAwait",
                filePath: filePath,
                node: method,
                sourceCode: sourceCode,
                bestPractice: "Async/await for non-blocking operations",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        // Pattern 2: ActionResult<T>
        var actionResultMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.ReturnType?.ToString().Contains("ActionResult") == true);

        foreach (var method in actionResultMethods)
        {
            var pattern = CreatePattern(
                name: $"{method.Identifier.Text}_ActionResult",
                type: PatternType.ApiDesign,
                category: PatternCategory.Operational,
                implementation: "ActionResult",
                filePath: filePath,
                node: method,
                sourceCode: sourceCode,
                bestPractice: "Strongly-typed API responses",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
