using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Configuration Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Configuration Patterns




    private List<CodePattern> DetectConfigurationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: IConfiguration usage
        var configCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Configuration[") || 
                         inv.ToString().Contains("Configuration.GetSection"));

        foreach (var config in configCalls)
        {
            var pattern = CreatePattern(
                name: "ConfigurationAccess",
                type: PatternType.Configuration,
                category: PatternCategory.Operational,
                implementation: "IConfiguration",
                filePath: filePath,
                node: config,
                sourceCode: sourceCode,
                bestPractice: "Configuration access (consider Options pattern)",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Confidence = 0.7f;
            pattern.Metadata["suggestion"] = "Consider using IOptions<T> for strongly-typed configuration";
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
