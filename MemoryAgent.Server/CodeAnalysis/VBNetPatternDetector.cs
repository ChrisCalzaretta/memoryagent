using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects coding patterns and best practices in VB.NET code
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";

    public string GetLanguage() => "VB.NET";

    public List<PatternType> GetSupportedPatternTypes() => Enum.GetValues<PatternType>().ToList();

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var lines = sourceCode.Split('\n');

            patterns.AddRange(DetectCachingPatterns(lines, filePath, context));
            patterns.AddRange(DetectRetryPatterns(lines, filePath, context));
            patterns.AddRange(DetectValidationPatterns(lines, filePath, context));
            patterns.AddRange(DetectDependencyInjectionPatterns(lines, filePath, context));
            patterns.AddRange(DetectLoggingPatterns(lines, filePath, context));
            patterns.AddRange(DetectErrorHandlingPatterns(lines, filePath, context));
            
            // VB.NET ATTRIBUTE PATTERNS (25 comprehensive patterns)
            patterns.AddRange(DetectRoutingAttributes(lines, filePath, context));
            patterns.AddRange(DetectAuthorizationAttributes(lines, filePath, context));
            patterns.AddRange(DetectValidationAttributes(lines, filePath, context));
            patterns.AddRange(DetectBlazorComponentAttributes(lines, filePath, context));
            patterns.AddRange(DetectCachingAttributes(lines, filePath, context));
            
            // AZURE ARCHITECTURE PATTERNS (36 patterns)
            // TODO: Implement DetectAzurePatternsVBNet
            // patterns.AddRange(DetectAzurePatternsVBNet(lines, filePath, context));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }
    
}
