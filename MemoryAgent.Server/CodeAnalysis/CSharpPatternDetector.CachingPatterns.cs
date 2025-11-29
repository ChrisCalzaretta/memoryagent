using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Caching Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Caching Patterns




    private List<CodePattern> DetectCachingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: IMemoryCache.TryGetValue
        var tryGetValueCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("TryGetValue") && 
                         (inv.ToString().Contains("_cache") || inv.ToString().Contains("cache")));

        foreach (var call in tryGetValueCalls)
        {
            var pattern = CreatePattern(
                name: "IMemoryCache_TryGetValue",
                type: PatternType.Caching,
                category: PatternCategory.Performance,
                implementation: "IMemoryCache",
                filePath: filePath,
                node: call,
                sourceCode: sourceCode,
                bestPractice: "In-memory caching with IMemoryCache for fast data access",
                azureUrl: AzureCachingUrl,
                context: context
            );
            
            pattern.Metadata["cache_type"] = "memory";
            pattern.Metadata["operation"] = "read";
            patterns.Add(pattern);
        }

        // Pattern 2: IMemoryCache.Set
        var setCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains(".Set(") &&
                         (inv.ToString().Contains("_cache") || inv.ToString().Contains("cache")));

        foreach (var call in setCalls)
        {
            var pattern = CreatePattern(
                name: "IMemoryCache_Set",
                type: PatternType.Caching,
                category: PatternCategory.Performance,
                implementation: "IMemoryCache",
                filePath: filePath,
                node: call,
                sourceCode: sourceCode,
                bestPractice: "In-memory caching with expiration policy",
                azureUrl: AzureCachingUrl,
                context: context
            );
            
            pattern.Metadata["cache_type"] = "memory";
            pattern.Metadata["operation"] = "write";
            patterns.Add(pattern);
        }

        // Pattern 3: IDistributedCache
        var distributedCacheCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => (inv.ToString().Contains("GetAsync") || inv.ToString().Contains("SetAsync")) &&
                         inv.ToString().Contains("distributedCache"));

        foreach (var call in distributedCacheCalls)
        {
            var pattern = CreatePattern(
                name: "IDistributedCache",
                type: PatternType.Caching,
                category: PatternCategory.Performance,
                implementation: "IDistributedCache",
                filePath: filePath,
                node: call,
                sourceCode: sourceCode,
                bestPractice: "Distributed caching (Redis) for scalable applications",
                azureUrl: AzureCachingUrl,
                context: context
            );
            
            pattern.Metadata["cache_type"] = "distributed";
            patterns.Add(pattern);
        }

        // Pattern 4: ResponseCache attribute
        var responseCacheAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().Contains("ResponseCache"));

        foreach (var attr in responseCacheAttrs)
        {
            var pattern = CreatePattern(
                name: "ResponseCache_Attribute",
                type: PatternType.Caching,
                category: PatternCategory.Performance,
                implementation: "ResponseCache",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: "HTTP response caching for API endpoints",
                azureUrl: AzureCachingUrl,
                context: context
            );
            
            pattern.Metadata["cache_type"] = "http_response";
            patterns.Add(pattern);
        }

        // Pattern 5: OutputCache attribute (ASP.NET Core 7+)
        var outputCacheAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().Contains("OutputCache"));

        foreach (var attr in outputCacheAttrs)
        {
            var pattern = CreatePattern(
                name: "OutputCache_Attribute",
                type: PatternType.Caching,
                category: PatternCategory.Performance,
                implementation: "OutputCache",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: "Modern output caching in ASP.NET Core",
                azureUrl: AzureCachingUrl,
                context: context
            );
            
            pattern.Metadata["cache_type"] = "output";
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
