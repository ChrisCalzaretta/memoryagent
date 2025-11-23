using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects coding patterns and best practices in VB.NET code
/// </summary>
public class VBNetPatternDetector : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";

    public string GetLanguage() => "VB.NET";

    public List<PatternType> GetSupportedPatternTypes() => new()
    {
        PatternType.Caching,
        PatternType.Resilience,
        PatternType.Validation,
        PatternType.DependencyInjection,
        PatternType.Logging,
        PatternType.ErrorHandling
    };

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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

    #region Caching Patterns

    private List<CodePattern> DetectCachingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: MemoryCache usage
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("_cache.Contains") || lines[i].Contains("_cache.Get"))
            {
                var pattern = CreatePattern(
                    name: "MemoryCache_Access",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "MemoryCache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "MemoryCache for in-memory caching",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "memory";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: HttpRuntime.Cache (legacy ASP.NET)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("HttpRuntime.Cache"))
            {
                var pattern = CreatePattern(
                    name: "HttpRuntime_Cache",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "HttpRuntime.Cache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Legacy ASP.NET caching (consider IMemoryCache)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Confidence = 0.8f;
                pattern.Metadata["legacy"] = true;
                pattern.Metadata["suggestion"] = "Upgrade to IMemoryCache for modern .NET";
                patterns.Add(pattern);
            }
        }

        // Pattern 3: OutputCache attribute
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("<OutputCache("))
            {
                var pattern = CreatePattern(
                    name: "OutputCache_Attribute",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "OutputCache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Output caching for web pages",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "output";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Retry Patterns

    private List<CodePattern> DetectRetryPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: For loop with retry logic
        for (int i = 0; i < lines.Length; i++)
        {
            if ((lines[i].Contains("For") && (lines[i].Contains("retry") || lines[i].Contains("attempt"))))
            {
                // Look for Try-Catch in next lines
                var hasTryCatch = false;
                for (int j = i; j < Math.Min(i + 15, lines.Length); j++)
                {
                    if (lines[j].Contains("Try"))
                    {
                        hasTryCatch = true;
                        break;
                    }
                }

                if (hasTryCatch)
                {
                    var pattern = CreatePattern(
                        name: "Manual_RetryLoop",
                        type: PatternType.Resilience,
                        category: PatternCategory.Reliability,
                        implementation: "ManualRetry",
                        filePath: filePath,
                        lineNumber: i + 1,
                        content: GetContext(lines, i, 10),
                        bestPractice: "Manual retry loop (consider using Polly)",
                        azureUrl: AzureRetryUrl,
                        context: context
                    );
                    
                    pattern.Confidence = 0.7f;
                    pattern.Metadata["suggestion"] = "Consider using Polly for robust retry logic";
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    #endregion

    #region Validation Patterns

    private List<CodePattern> DetectValidationPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Data Annotations
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("<Required") || lines[i].Contains("<StringLength") || 
                lines[i].Contains("<Range") || lines[i].Contains("<EmailAddress"))
            {
                var attrName = ExtractAttributeName(lines[i]);
                var pattern = CreatePattern(
                    name: $"DataAnnotation_{attrName}",
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "DataAnnotations",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Input validation using {attrName} attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["validation_type"] = attrName;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Manual validation
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("String.IsNullOrEmpty") || lines[i].Contains("String.IsNullOrWhiteSpace"))
            {
                var pattern = CreatePattern(
                    name: "Manual_Validation",
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "ManualValidation",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Manual string validation",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Confidence = 0.6f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Dependency Injection Patterns

    private List<CodePattern> DetectDependencyInjectionPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Constructor with service parameters
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Public Sub New(") && lines[i].Contains("As I"))
            {
                var pattern = CreatePattern(
                    name: "Constructor_Injection",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "ConstructorInjection",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Constructor dependency injection",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Logging Patterns

    private List<CodePattern> DetectLoggingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: ILogger usage
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("_logger.Log") || lines[i].Contains("_logger.Info") ||
                lines[i].Contains("_logger.Warning") || lines[i].Contains("_logger.Error"))
            {
                var pattern = CreatePattern(
                    name: "Logger_Call",
                    type: PatternType.Logging,
                    category: PatternCategory.Operational,
                    implementation: "ILogger",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Logging with ILogger",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Error Handling Patterns

    private List<CodePattern> DetectErrorHandlingPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Try-Catch blocks
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals("Try", StringComparison.OrdinalIgnoreCase))
            {
                // Look for logging in Catch block
                var logsException = false;
                for (int j = i; j < Math.Min(i + 30, lines.Length); j++)
                {
                    if (lines[j].Contains("_logger.") || lines[j].Contains("Log."))
                    {
                        logsException = true;
                        break;
                    }
                    if (lines[j].Trim().Equals("End Try", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                var pattern = CreatePattern(
                    name: "TryCatch_Block",
                    type: PatternType.ErrorHandling,
                    category: PatternCategory.Reliability,
                    implementation: "TryCatch",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 15),
                    bestPractice: logsException ? "Exception handling with logging" : "Exception handling (add logging)",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["logs_exception"] = logsException;
                pattern.Confidence = logsException ? 1.0f : 0.6f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        int lineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "VB.NET",
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    private string GetContext(string[] lines, int centerLine, int contextLines)
    {
        var start = Math.Max(0, centerLine - contextLines);
        var end = Math.Min(lines.Length - 1, centerLine + contextLines);
        
        var contextList = new List<string>();
        for (int i = start; i <= end; i++)
        {
            contextList.Add(lines[i]);
        }
        
        return string.Join("\n", contextList).Trim();
    }

    private string ExtractAttributeName(string line)
    {
        var match = Regex.Match(line, @"<(\w+)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    #endregion
}

