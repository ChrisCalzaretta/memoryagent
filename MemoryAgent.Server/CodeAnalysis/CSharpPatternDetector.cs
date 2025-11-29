using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects coding patterns and best practices in C# code
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    protected const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    protected const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    protected const string AzureMonitoringUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring";
    protected const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";

    public string GetLanguage() => "C#";

    public List<PatternType> GetSupportedPatternTypes() => new()
    {
        PatternType.Caching,
        PatternType.Resilience,
        PatternType.Validation,
        PatternType.DependencyInjection,
        PatternType.Logging,
        PatternType.ErrorHandling,
        PatternType.Security,
        PatternType.Configuration,
        PatternType.ApiDesign
    };

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // Detect all pattern types
            patterns.AddRange(DetectCachingPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRetryPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectValidationPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDependencyInjectionPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLoggingPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectErrorHandlingPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSecurityPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConfigurationPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectApiDesignPatterns(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            // Log but don't fail indexing
            Console.WriteLine($"Error detecting patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

}
