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
            patterns.AddRange(DetectAzureWebPubSubPatterns(sourceCode, lines, filePath, context));
            
            // Azure Architecture Patterns
            patterns.AddRange(DetectAzureArchitecturePatternsPython(sourceCode, lines, filePath, context));
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

    #region Azure Web PubSub Patterns

    private List<CodePattern> DetectAzureWebPubSubPatterns(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        const string WebPubSubUrl = "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Pattern 1: WebPubSubServiceClient initialization
            if (Regex.IsMatch(line, @"WebPubSubServiceClient\s*\.\s*from_connection_string\s*\(", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(line, @"WebPubSubServiceClient\s*\(", RegexOptions.IgnoreCase))
            {
                var usesConfig = Regex.IsMatch(line, @"os\.environ|config|settings", RegexOptions.IgnoreCase) ||
                                lines.Skip(Math.Max(0, i - 3)).Take(5).Any(l => Regex.IsMatch(l, @"os\.environ|config|settings", RegexOptions.IgnoreCase));
                
                var pattern = CreatePattern(
                    name: "WebPubSubServiceClient_Init",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "WebPubSubServiceClient",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: usesConfig
                        ? "Service client initialized with connection string from configuration"
                        : "Service client initialized (WARNING: Use environment variables or config, not hardcoded strings)",
                    azureUrl: WebPubSubUrl,
                    context: context
                );
                pattern.Metadata["UsesConfiguration"] = usesConfig;
                patterns.Add(pattern);
            }

            // Pattern 2: send_to_all - Broadcast messaging
            if (Regex.IsMatch(line, @"\.send_to_all\s*\(|\.send_to_all_async\s*\(", RegexOptions.IgnoreCase))
            {
                var isAsync = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                var hasTryExcept = lines.Skip(Math.Max(0, i - 5)).Take(10).Any(l => 
                    Regex.IsMatch(l, @"^\s*try\s*:", RegexOptions.IgnoreCase));

                var pattern = CreatePattern(
                    name: "WebPubSub_BroadcastMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "send_to_all",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Broadcasting to all clients{(isAsync ? " (async)" : "")}{(hasTryExcept ? "" : " WARNING: Add try/except")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                );
                pattern.Metadata["IsAsync"] = isAsync;
                pattern.Metadata["HasErrorHandling"] = hasTryExcept;
                patterns.Add(pattern);
            }

            // Pattern 3: send_to_group - Group messaging
            if (Regex.IsMatch(line, @"\.send_to_group\s*\(|\.send_to_group_async\s*\(", RegexOptions.IgnoreCase))
            {
                var isAsync = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_GroupMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "send_to_group",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Sending to specific group{(isAsync ? " (async)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 4: send_to_user - User messaging
            if (Regex.IsMatch(line, @"\.send_to_user\s*\(|\.send_to_user_async\s*\(", RegexOptions.IgnoreCase))
            {
                var isAsync = Regex.IsMatch(line, @"\bawait\b", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_UserMessage",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.RealtimeMessaging,
                    implementation: "send_to_user",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Sending to specific user{(isAsync ? " (async)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 5: get_client_access_token - Token generation
            if (Regex.IsMatch(line, @"\.get_client_access_token\s*\(", RegexOptions.IgnoreCase))
            {
                var hasUserId = Regex.IsMatch(line, @"user_id\s*=", RegexOptions.IgnoreCase);
                var hasRoles = Regex.IsMatch(line, @"roles\s*=", RegexOptions.IgnoreCase);
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_ClientAccessToken",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.Security,
                    implementation: "get_client_access_token",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Generating client access token{(hasUserId ? " with user ID" : "")}{(hasRoles ? " and roles" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 6: add_connection_to_group - Group management
            if (Regex.IsMatch(line, @"\.add_connection_to_group\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_AddToGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "add_connection_to_group",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Adding connection to group for targeted messaging",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 7: remove_connection_from_group
            if (Regex.IsMatch(line, @"\.remove_connection_from_group\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_RemoveFromGroup",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "remove_connection_from_group",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Removing connection from group",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 8: close_connection
            if (Regex.IsMatch(line, @"\.close_connection\s*\(", RegexOptions.IgnoreCase))
            {
                patterns.Add(CreatePattern(
                    name: "WebPubSub_CloseConnection",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.ConnectionManagement,
                    implementation: "close_connection",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: "Gracefully closing client connection",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 9: DefaultAzureCredential / ManagedIdentityCredential
            if (Regex.IsMatch(line, @"DefaultAzureCredential\s*\(|ManagedIdentityCredential\s*\(", RegexOptions.IgnoreCase))
            {
                var usesManagedIdentity = line.Contains("ManagedIdentityCredential");
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_Authentication",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.Security,
                    implementation: usesManagedIdentity ? "ManagedIdentityCredential" : "DefaultAzureCredential",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 5),
                    bestPractice: $"Using Azure AD authentication{(usesManagedIdentity ? " with Managed Identity (recommended)" : "")}",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }

            // Pattern 10: Event Handler webhook endpoints (Flask/FastAPI)
            if ((Regex.IsMatch(line, @"@app\.route\s*\(.*POST", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(line, @"@router\.post\s*\(", RegexOptions.IgnoreCase)) &&
                lines.Skip(i).Take(10).Any(l => Regex.IsMatch(l, @"webpubsub|event", RegexOptions.IgnoreCase)))
            {
                var hasValidation = lines.Skip(i).Take(15).Any(l => 
                    Regex.IsMatch(l, @"validate|verify.*signature", RegexOptions.IgnoreCase));
                
                patterns.Add(CreatePattern(
                    name: "WebPubSub_WebhookEndpoint",
                    type: PatternType.AzureWebPubSub,
                    category: PatternCategory.EventHandlers,
                    implementation: "HTTP Webhook",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 10),
                    bestPractice: hasValidation 
                        ? "Webhook endpoint with signature validation" 
                        : "Webhook endpoint (CRITICAL: Must validate signatures!)",
                    azureUrl: WebPubSubUrl,
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS (All 36 Patterns)

    private List<CodePattern> DetectAzureArchitecturePatternsPython(string sourceCode, string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern detection using keywords and structure analysis
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // CQRS
            if ((line.Contains("Command") || line.Contains("Query")) && line.Contains("class"))
                patterns.Add(CreatePattern("CQRS_Pattern", PatternType.CQRS, PatternCategory.DataManagement,
                    "CQRS", filePath, i + 1, GetContext(lines, i, 2),
                    "CQRS: Separate read/write", "https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs", context));
            
            // Event Sourcing
            if (line.Contains("EventStore") || line.Contains("DomainEvent"))
                patterns.Add(CreatePattern("EventSourcing_Pattern", PatternType.EventSourcing, PatternCategory.DataManagement,
                    "Event Sourcing", filePath, i + 1, GetContext(lines, i, 2),
                    "Event Sourcing: Event-based state", "https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing", context));
            
            // Circuit Breaker
            if (line.Contains("CircuitBreaker") || line.Contains("circuit_breaker") || line.Contains("@circuit"))
                patterns.Add(CreatePattern("CircuitBreaker_Pattern", PatternType.CircuitBreaker, PatternCategory.ResiliencyPatterns,
                    "Circuit Breaker", filePath, i + 1, GetContext(lines, i, 2),
                    "Circuit Breaker: Fail fast", "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker", context));
            
            // Bulkhead
            if (line.Contains("Semaphore") || line.Contains("BoundedSemaphore") || line.Contains("bulkhead"))
                patterns.Add(CreatePattern("Bulkhead_Pattern", PatternType.Bulkhead, PatternCategory.ResiliencyPatterns,
                    "Bulkhead", filePath, i + 1, GetContext(lines, i, 2),
                    "Bulkhead: Resource isolation", "https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead", context));
            
            // Saga
            if (line.Contains("Saga") || line.Contains("saga"))
                patterns.Add(CreatePattern("Saga_Pattern", PatternType.Saga, PatternCategory.OperationalPatterns,
                    "Saga", filePath, i + 1, GetContext(lines, i, 2),
                    "Saga: Distributed transactions", "https://learn.microsoft.com/en-us/azure/architecture/patterns/saga", context));
            
            // Ambassador
            if ((line.Contains("Ambassador") || line.Contains("Proxy")) && line.Contains("class"))
                patterns.Add(CreatePattern("Ambassador_Pattern", PatternType.Ambassador, PatternCategory.DesignImplementation,
                    "Ambassador", filePath, i + 1, GetContext(lines, i, 2),
                    "Ambassador: Network proxy", "https://learn.microsoft.com/en-us/azure/architecture/patterns/ambassador", context));
            
            // Anti-Corruption Layer
            if (line.Contains("Adapter") || line.Contains("Facade") || line.Contains("LegacyAdapter"))
                patterns.Add(CreatePattern("AntiCorruptionLayer_Pattern", PatternType.AntiCorruptionLayer, PatternCategory.DesignImplementation,
                    "Anti-Corruption Layer", filePath, i + 1, GetContext(lines, i, 2),
                    "Anti-Corruption: Legacy isolation", "https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer", context));
            
            // Backends for Frontends
            if ((line.Contains("BFF") || line.Contains("MobileAPI") || line.Contains("WebAPI")) && line.Contains("class"))
                patterns.Add(CreatePattern("BFF_Pattern", PatternType.BackendsForFrontends, PatternCategory.DesignImplementation,
                    "BFF", filePath, i + 1, GetContext(lines, i, 2),
                    "BFF: Client-specific backends", "https://learn.microsoft.com/en-us/azure/architecture/patterns/backends-for-frontends", context));
            
            // Choreography
            if (line.Contains("EventHandler") || line.Contains("on_event"))
                patterns.Add(CreatePattern("Choreography_Pattern", PatternType.Choreography, PatternCategory.MessagingPatterns,
                    "Choreography", filePath, i + 1, GetContext(lines, i, 2),
                    "Choreography: Event-driven", "https://learn.microsoft.com/en-us/azure/architecture/patterns/choreography", context));
            
            // Claim Check
            if ((line.Contains("blob") && sourceCode.Contains("queue")) || line.Contains("claim_check"))
                patterns.Add(CreatePattern("ClaimCheck_Pattern", PatternType.ClaimCheck, PatternCategory.MessagingPatterns,
                    "Claim Check", filePath, i + 1, GetContext(lines, i, 2),
                    "Claim Check: Large message handling", "https://learn.microsoft.com/en-us/azure/architecture/patterns/claim-check", context));
            
            // Compensating Transaction  
            if (line.Contains("compensate") || line.Contains("rollback") || line.Contains("undo"))
                patterns.Add(CreatePattern("CompensatingTransaction_Pattern", PatternType.CompensatingTransaction, PatternCategory.ResiliencyPatterns,
                    "Compensating Transaction", filePath, i + 1, GetContext(lines, i, 2),
                    "Compensating: Undo operations", "https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction", context));
            
            // Competing Consumers
            if ((line.Contains("consumer") || line.Contains("worker")) && (sourceCode.Contains("queue") || sourceCode.Contains("async")))
                patterns.Add(CreatePattern("CompetingConsumers_Pattern", PatternType.CompetingConsumers, PatternCategory.MessagingPatterns,
                    "Competing Consumers", filePath, i + 1, GetContext(lines, i, 2),
                    "Competing Consumers: Parallel processing", "https://learn.microsoft.com/en-us/azure/architecture/patterns/competing-consumers", context));
            
            // Materialized View
            if (line.Contains("ReadModel") || line.Contains("View") || line.Contains("denormalized"))
                patterns.Add(CreatePattern("MaterializedView_Pattern", PatternType.MaterializedView, PatternCategory.DataManagement,
                    "Materialized View", filePath, i + 1, GetContext(lines, i, 2),
                    "Materialized View: Precomputed data", "https://learn.microsoft.com/en-us/azure/architecture/patterns/materialized-view", context));
            
            // Gateway Aggregation
            if (line.Contains("aggregate") || (sourceCode.Split(new[] { "requests.get", "requests.post" }, StringSplitOptions.None).Length > 3))
                patterns.Add(CreatePattern("GatewayAggregation_Pattern", PatternType.GatewayAggregation, PatternCategory.DesignImplementation,
                    "Gateway Aggregation", filePath, i + 1, GetContext(lines, i, 2),
                    "Gateway: Aggregate requests", "https://learn.microsoft.com/en-us/azure/architecture/patterns/gateway-aggregation", context));
            
            // Gateway Offloading
            if (line.Contains("middleware") || line.Contains("@app.middleware"))
                patterns.Add(CreatePattern("GatewayOffloading_Pattern", PatternType.GatewayOffloading, PatternCategory.DesignImplementation,
                    "Gateway Offloading", filePath, i + 1, GetContext(lines, i, 2),
                    "Gateway: Offload concerns", "https://learn.microsoft.com/en-us/azure/architecture/patterns/gateway-offloading", context));
            
            // Throttling
            if (line.Contains("rate_limit") || line.Contains("throttle") || line.Contains("@limiter"))
                patterns.Add(CreatePattern("Throttling_Pattern", PatternType.Throttling, PatternCategory.ResiliencyPatterns,
                    "Throttling", filePath, i + 1, GetContext(lines, i, 2),
                    "Throttling: Rate limiting", "https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling", context));
            
            // Federated Identity
            if (line.Contains("OAuth") || line.Contains("jwt") || line.Contains("OIDC"))
                patterns.Add(CreatePattern("FederatedIdentity_Pattern", PatternType.FederatedIdentity, PatternCategory.SecurityPatterns,
                    "Federated Identity", filePath, i + 1, GetContext(lines, i, 2),
                    "Federated: External auth", "https://learn.microsoft.com/en-us/azure/architecture/patterns/federated-identity", context));
            
            // Priority Queue
            if (line.Contains("PriorityQueue") || line.Contains("heapq"))
                patterns.Add(CreatePattern("PriorityQueue_Pattern", PatternType.PriorityQueue, PatternCategory.MessagingPatterns,
                    "Priority Queue", filePath, i + 1, GetContext(lines, i, 2),
                    "Priority Queue: Ordered processing", "https://learn.microsoft.com/en-us/azure/architecture/patterns/priority-queue", context));
            
            // Pipes and Filters
            if (line.Contains("Pipeline") || line.Contains("Filter"))
                patterns.Add(CreatePattern("PipesAndFilters_Pattern", PatternType.PipesAndFilters, PatternCategory.MessagingPatterns,
                    "Pipes and Filters", filePath, i + 1, GetContext(lines, i, 2),
                    "Pipes: Processing pipeline", "https://learn.microsoft.com/en-us/azure/architecture/patterns/pipes-and-filters", context));
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

