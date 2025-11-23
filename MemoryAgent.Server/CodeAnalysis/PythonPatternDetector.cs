using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects coding patterns and best practices in Python code
/// </summary>
public class PythonPatternDetector : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureMonitoringUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";

    public string GetLanguage() => "Python";

    public List<PatternType> GetSupportedPatternTypes() => new()
    {
        PatternType.Caching,
        PatternType.Resilience,
        PatternType.Validation,
        PatternType.DependencyInjection,
        PatternType.Logging,
        PatternType.ErrorHandling,
        PatternType.ApiDesign
    };

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var lines = sourceCode.Split('\n');

            patterns.AddRange(DetectCachingPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectRetryPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectValidationPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectDependencyInjectionPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectLoggingPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectErrorHandlingPatterns(sourceCode, lines, filePath, context));
            patterns.AddRange(DetectApiDesignPatterns(sourceCode, lines, filePath, context));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

    #region Caching Patterns

    private List<CodePattern> DetectCachingPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: @lru_cache decorator
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@lru_cache") || lines[i].Contains("@cache"))
            {
                var funcName = i + 1 < lines.Length ? ExtractFunctionName(lines[i + 1]) : "unknown";
                var pattern = CreatePattern(
                    name: $"{funcName}_lru_cache",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "functools.lru_cache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Function memoization with LRU cache",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "function_decorator";
                pattern.Metadata["library"] = "functools";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: @cached decorator (cachetools)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@cached"))
            {
                var funcName = i + 1 < lines.Length ? ExtractFunctionName(lines[i + 1]) : "unknown";
                var pattern = CreatePattern(
                    name: $"{funcName}_cached",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "cachetools.cached",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Cachetools flexible caching decorator",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "cachetools";
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Redis usage
        var redisPattern = new Regex(@"redis\.(get|set|setex)", RegexOptions.IgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            if (redisPattern.IsMatch(lines[i]))
            {
                var pattern = CreatePattern(
                    name: "Redis_Cache",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "redis",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Redis distributed caching",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_type"] = "distributed";
                pattern.Metadata["library"] = "redis";
                patterns.Add(pattern);
            }
        }

        // Pattern 4: Django cache
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("cache.get") || lines[i].Contains("cache.set"))
            {
                var pattern = CreatePattern(
                    name: "Django_Cache",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "django.core.cache",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Django framework caching",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["framework"] = "Django";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Retry Patterns

    private List<CodePattern> DetectRetryPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: @retry decorator (tenacity)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@retry"))
            {
                var funcName = i + 1 < lines.Length ? ExtractFunctionName(lines[i + 1]) : "unknown";
                var pattern = CreatePattern(
                    name: $"{funcName}_retry",
                    type: PatternType.Resilience,
                    category: PatternCategory.Reliability,
                    implementation: "tenacity.retry",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Tenacity retry decorator for resilience",
                    azureUrl: AzureRetryUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "tenacity";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: @backoff decorator
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@backoff.on_exception"))
            {
                var funcName = i + 1 < lines.Length ? ExtractFunctionName(lines[i + 1]) : "unknown";
                var pattern = CreatePattern(
                    name: $"{funcName}_backoff",
                    type: PatternType.Resilience,
                    category: PatternCategory.Reliability,
                    implementation: "backoff",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Backoff exponential retry",
                    azureUrl: AzureRetryUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "backoff";
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Manual retry loops
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("for") && (lines[i].Contains("retry") || lines[i].Contains("attempt")))
            {
                // Look for try-except in next few lines
                var hasTryExcept = false;
                for (int j = i; j < Math.Min(i + 10, lines.Length); j++)
                {
                    if (lines[j].Contains("try:"))
                    {
                        hasTryExcept = true;
                        break;
                    }
                }

                if (hasTryExcept)
                {
                    var pattern = CreatePattern(
                        name: "Manual_RetryLoop",
                        type: PatternType.Resilience,
                        category: PatternCategory.Reliability,
                        implementation: "ManualRetry",
                        filePath: filePath,
                        lineNumber: i + 1,
                        content: GetContext(lines, i, 10),
                        bestPractice: "Manual retry loop (consider using tenacity)",
                        azureUrl: AzureRetryUrl,
                        context: context
                    );
                    
                    pattern.Confidence = 0.7f;
                    pattern.Metadata["suggestion"] = "Consider using tenacity library";
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    #endregion

    #region Validation Patterns

    private List<CodePattern> DetectValidationPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Pydantic BaseModel
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"class\s+(\w+)\(BaseModel\)");
            if (match.Success)
            {
                var className = match.Groups[1].Value;
                var pattern = CreatePattern(
                    name: className,
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "pydantic.BaseModel",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: "Pydantic data validation and parsing",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "pydantic";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Pydantic @validator
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@validator"))
            {
                var funcName = i + 1 < lines.Length ? ExtractFunctionName(lines[i + 1]) : "unknown";
                var pattern = CreatePattern(
                    name: $"{funcName}_validator",
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "pydantic.validator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Custom Pydantic field validator",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "pydantic";
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Marshmallow Schema
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"class\s+(\w+)\(.*Schema\)");
            if (match.Success && lines[i].Contains("Schema"))
            {
                var className = match.Groups[1].Value;
                var pattern = CreatePattern(
                    name: className,
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "marshmallow.Schema",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: "Marshmallow serialization and validation",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "marshmallow";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Dependency Injection Patterns

    private List<CodePattern> DetectDependencyInjectionPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: FastAPI Depends
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Depends("))
            {
                var pattern = CreatePattern(
                    name: "FastAPI_Depends",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "fastapi.Depends",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "FastAPI dependency injection",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["framework"] = "FastAPI";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: dependency_injector
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("containers.DeclarativeContainer") || 
                lines[i].Contains("providers."))
            {
                var pattern = CreatePattern(
                    name: "DependencyInjector",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "dependency_injector",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Dependency injection container",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "dependency_injector";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Logging Patterns

    private List<CodePattern> DetectLoggingPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: logger.info/warning/error with extra
        for (int i = 0; i < lines.Length; i++)
        {
            if ((lines[i].Contains("logger.info") || lines[i].Contains("logger.warning") || lines[i].Contains("logger.error")) &&
                lines[i].Contains("extra="))
            {
                var pattern = CreatePattern(
                    name: "StructuredLogging",
                    type: PatternType.Logging,
                    category: PatternCategory.Operational,
                    implementation: "logging.Logger",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Structured logging with extra fields",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["structured"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: structlog
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("structlog.get_logger"))
            {
                var pattern = CreatePattern(
                    name: "Structlog",
                    type: PatternType.Logging,
                    category: PatternCategory.Operational,
                    implementation: "structlog",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Structlog structured logging library",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "structlog";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Error Handling Patterns

    private List<CodePattern> DetectErrorHandlingPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: try-except blocks
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().StartsWith("try:"))
            {
                // Look for logging in except block
                var logsException = false;
                for (int j = i; j < Math.Min(i + 20, lines.Length); j++)
                {
                    if (lines[j].Contains("logger.") || lines[j].Contains("logging."))
                    {
                        logsException = true;
                        break;
                    }
                    if (lines[j].Trim().StartsWith("def ") || lines[j].Trim().StartsWith("class "))
                    {
                        break;
                    }
                }

                var pattern = CreatePattern(
                    name: "TryExcept_Block",
                    type: PatternType.ErrorHandling,
                    category: PatternCategory.Reliability,
                    implementation: "TryExcept",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: logsException ? "Exception handling with logging" : "Exception handling (add logging)",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["logs_exception"] = logsException;
                pattern.Confidence = logsException ? 1.0f : 0.6f;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Custom exceptions
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"class\s+(\w+Exception)\(");
            if (match.Success)
            {
                var className = match.Groups[1].Value;
                var pattern = CreatePattern(
                    name: className,
                    type: PatternType.ErrorHandling,
                    category: PatternCategory.Reliability,
                    implementation: "CustomException",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Custom exception for domain errors",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        // Pattern 3: FastAPI exception handlers
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@app.exception_handler"))
            {
                var pattern = CreatePattern(
                    name: "ExceptionHandler",
                    type: PatternType.ErrorHandling,
                    category: PatternCategory.Reliability,
                    implementation: "FastAPI.exception_handler",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Global exception handler in FastAPI",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["framework"] = "FastAPI";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region API Design Patterns

    private List<CodePattern> DetectApiDesignPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: async def (async functions)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("async def "))
            {
                var funcName = ExtractFunctionName(lines[i]);
                var pattern = CreatePattern(
                    name: $"{funcName}_async",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "AsyncFunction",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Async function for non-blocking I/O",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        // Pattern 2: FastAPI route decorators
        var routePattern = new Regex(@"@(app|router)\.(get|post|put|delete|patch)");
        for (int i = 0; i < lines.Length; i++)
        {
            var match = routePattern.Match(lines[i]);
            if (match.Success)
            {
                var method = match.Groups[2].Value.ToUpper();
                var pattern = CreatePattern(
                    name: $"FastAPI_{method}_Endpoint",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "FastAPI.route",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"FastAPI {method} endpoint",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["http_method"] = method;
                pattern.Metadata["framework"] = "FastAPI";
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
            Language = "Python",
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

    private string ExtractFunctionName(string line)
    {
        var match = Regex.Match(line, @"def\s+(\w+)\s*\(");
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    #endregion
}

