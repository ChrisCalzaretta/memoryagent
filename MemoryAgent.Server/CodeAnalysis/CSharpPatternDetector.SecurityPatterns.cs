using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Security Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Security Patterns




    private List<CodePattern> DetectSecurityPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: [Authorize] attribute
        var authorizeAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().Contains("Authorize"));

        foreach (var attr in authorizeAttrs)
        {
            var pattern = CreatePattern(
                name: "Authorize_Attribute",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "AuthorizeAttribute",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: "Authorization protection on endpoints",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        // Pattern 2: [ValidateAntiForgeryToken]
        var antiForgeryAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().Contains("ValidateAntiForgeryToken"));

        foreach (var attr in antiForgeryAttrs)
        {
            var pattern = CreatePattern(
                name: "AntiForgeryToken",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "ValidateAntiForgeryToken",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: "CSRF protection with anti-forgery tokens",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
