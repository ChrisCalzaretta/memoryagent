using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects coding patterns and best practices in C# code
/// </summary>
public class CSharpPatternDetector : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureMonitoringUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";

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

    #region Resilience/Retry Patterns

    private List<CodePattern> DetectRetryPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Polly Policy.Handle
        var pollyPolicies = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Policy.Handle") || 
                         inv.ToString().Contains("Policy.HandleResult"));

        foreach (var policy in pollyPolicies)
        {
            var pattern = CreatePattern(
                name: "Polly_Policy",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly",
                filePath: filePath,
                node: policy,
                sourceCode: sourceCode,
                bestPractice: "Polly resilience policies for transient fault handling",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Polly";
            patterns.Add(pattern);
        }

        // Pattern 2: WaitAndRetry
        var retryPolicies = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("WaitAndRetry") || 
                         inv.ToString().Contains("RetryAsync"));

        foreach (var retry in retryPolicies)
        {
            var pattern = CreatePattern(
                name: "Polly_RetryPolicy",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly.Retry",
                filePath: filePath,
                node: retry,
                sourceCode: sourceCode,
                bestPractice: "Exponential backoff retry policy",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["retry_type"] = "exponential_backoff";
            patterns.Add(pattern);
        }

        // Pattern 3: Circuit Breaker
        var circuitBreakers = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("CircuitBreaker"));

        foreach (var cb in circuitBreakers)
        {
            var pattern = CreatePattern(
                name: "Polly_CircuitBreaker",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly.CircuitBreaker",
                filePath: filePath,
                node: cb,
                sourceCode: sourceCode,
                bestPractice: "Circuit breaker pattern for cascading failure prevention",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["pattern"] = "circuit_breaker";
            patterns.Add(pattern);
        }

        // Pattern 4: Manual retry loops
        var forLoops = root.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Where(f => f.ToString().Contains("retry") || f.ToString().Contains("attempt"));

        foreach (var loop in forLoops)
        {
            // Check if it has try-catch inside
            var hasTryCatch = loop.DescendantNodes().OfType<TryStatementSyntax>().Any();
            if (hasTryCatch)
            {
                var pattern = CreatePattern(
                    name: "Manual_RetryLoop",
                    type: PatternType.Resilience,
                    category: PatternCategory.Reliability,
                    implementation: "ManualRetry",
                    filePath: filePath,
                    node: loop,
                    sourceCode: sourceCode,
                    bestPractice: "Manual retry loop (consider using Polly)",
                    azureUrl: AzureRetryUrl,
                    context: context
                );
                
                pattern.Confidence = 0.7f; // Lower confidence for manual detection
                pattern.Metadata["suggestion"] = "Consider using Polly for robust retry logic";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Validation Patterns

    private List<CodePattern> DetectValidationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Data Annotations ([Required], [Range], [EmailAddress], etc.)
        var validationAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString() is "Required" or "Range" or "EmailAddress" or 
                          "StringLength" or "RegularExpression" or "MinLength" or "MaxLength" or 
                          "Phone" or "CreditCard" or "Url");

        foreach (var attr in validationAttrs)
        {
            var pattern = CreatePattern(
                name: $"DataAnnotation_{attr.Name}",
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "DataAnnotations",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: $"Input validation using {attr.Name} attribute",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["validation_type"] = attr.Name.ToString();
            pattern.Metadata["library"] = "System.ComponentModel.DataAnnotations";
            patterns.Add(pattern);
        }

        // Pattern 2: FluentValidation
        var fluentValidators = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("AbstractValidator")) == true);

        foreach (var validator in fluentValidators)
        {
            var pattern = CreatePattern(
                name: validator.Identifier.Text,
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "FluentValidation",
                filePath: filePath,
                node: validator,
                sourceCode: sourceCode,
                bestPractice: "Fluent validation for complex business rules",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "FluentValidation";
            patterns.Add(pattern);
        }

        // Pattern 3: Guard clauses
        var guardCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Guard.Against"));

        foreach (var guard in guardCalls)
        {
            var pattern = CreatePattern(
                name: "Guard_Clause",
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "GuardClauses",
                filePath: filePath,
                node: guard,
                sourceCode: sourceCode,
                bestPractice: "Guard clauses for defensive programming",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Ardalis.GuardClauses";
            patterns.Add(pattern);
        }

        return patterns;
    }

    #endregion

    #region Dependency Injection Patterns

    private List<CodePattern> DetectDependencyInjectionPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Constructor injection
        var constructorsWithParams = root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count > 0);

        foreach (var ctor in constructorsWithParams)
        {
            // Check if parameters are interfaces or have common service suffixes
            var hasServiceParams = ctor.ParameterList.Parameters.Any(p => 
                p.Type?.ToString().StartsWith("I") == true || // Interface
                p.Type?.ToString().Contains("Service") == true ||
                p.Type?.ToString().Contains("Repository") == true ||
                p.Type?.ToString().Contains("Logger") == true ||
                p.Type?.ToString().Contains("Options") == true);

            if (hasServiceParams)
            {
                var className = ctor.Parent is ClassDeclarationSyntax classDecl ? classDecl.Identifier.Text : "Unknown";
                var pattern = CreatePattern(
                    name: $"{className}_ConstructorInjection",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "ConstructorInjection",
                    filePath: filePath,
                    node: ctor,
                    sourceCode: sourceCode,
                    bestPractice: "Constructor dependency injection",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["dependency_count"] = ctor.ParameterList.Parameters.Count;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Service registration (AddScoped, AddSingleton, AddTransient)
        var serviceRegistrations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("AddScoped") || 
                         inv.ToString().Contains("AddSingleton") ||
                         inv.ToString().Contains("AddTransient"));

        foreach (var reg in serviceRegistrations)
        {
            var lifetimeType = reg.ToString().Contains("AddScoped") ? "Scoped" :
                             reg.ToString().Contains("AddSingleton") ? "Singleton" : "Transient";
            
            var pattern = CreatePattern(
                name: $"ServiceRegistration_{lifetimeType}",
                type: PatternType.DependencyInjection,
                category: PatternCategory.Operational,
                implementation: $"ServiceCollection.Add{lifetimeType}",
                filePath: filePath,
                node: reg,
                sourceCode: sourceCode,
                bestPractice: $"Service registration with {lifetimeType} lifetime",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["lifetime"] = lifetimeType;
            patterns.Add(pattern);
        }

        // Pattern 3: Options pattern
        var optionsParams = root.DescendantNodes()
            .OfType<ParameterSyntax>()
            .Where(p => p.Type?.ToString().Contains("IOptions") == true);

        foreach (var param in optionsParams)
        {
            var pattern = CreatePattern(
                name: "Options_Pattern",
                type: PatternType.Configuration,
                category: PatternCategory.Operational,
                implementation: "IOptions",
                filePath: filePath,
                node: param,
                sourceCode: sourceCode,
                bestPractice: "Options pattern for configuration",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["pattern"] = "options";
            patterns.Add(pattern);
        }

        return patterns;
    }

    #endregion

    #region Logging Patterns

    private List<CodePattern> DetectLoggingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: ILogger structured logging
        var loggerCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("_logger.Log") || 
                         inv.ToString().Contains("_logger.LogInformation") ||
                         inv.ToString().Contains("_logger.LogWarning") ||
                         inv.ToString().Contains("_logger.LogError"));

        foreach (var log in loggerCalls)
        {
            // Check if it's structured logging (has placeholders like {UserId})
            var isStructured = log.ToString().Contains("{") && log.ToString().Contains("}");
            
            var pattern = CreatePattern(
                name: isStructured ? "StructuredLogging" : "BasicLogging",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "ILogger",
                filePath: filePath,
                node: log,
                sourceCode: sourceCode,
                bestPractice: isStructured ? "Structured logging with ILogger" : "Basic logging (consider structured logging)",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["structured"] = isStructured;
            pattern.Confidence = isStructured ? 1.0f : 0.7f;
            patterns.Add(pattern);
        }

        // Pattern 2: Log.Information (Serilog)
        var serilogCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().StartsWith("Log."));

        foreach (var log in serilogCalls)
        {
            var pattern = CreatePattern(
                name: "Serilog_Logging",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "Serilog",
                filePath: filePath,
                node: log,
                sourceCode: sourceCode,
                bestPractice: "Serilog structured logging",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Serilog";
            patterns.Add(pattern);
        }

        // Pattern 3: BeginScope
        var scopeCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("BeginScope"));

        foreach (var scope in scopeCalls)
        {
            var pattern = CreatePattern(
                name: "LogScope",
                type: PatternType.Logging,
                category: PatternCategory.Operational,
                implementation: "ILogger.BeginScope",
                filePath: filePath,
                node: scope,
                sourceCode: sourceCode,
                bestPractice: "Log scopes for contextual logging",
                azureUrl: AzureMonitoringUrl,
                context: context
            );
            
            pattern.Metadata["feature"] = "log_scope";
            patterns.Add(pattern);
        }

        return patterns;
    }

    #endregion

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

    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        SyntaxNode node,
        string sourceCode,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var endLineNumber = lineSpan.EndLinePosition.Line + 1;

        // Get context (surrounding code)
        var content = GetCodeContext(node, sourceCode, lineNumber, endLineNumber);

        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = endLineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    private string GetCodeContext(SyntaxNode node, string sourceCode, int startLine, int endLine)
    {
        // Get surrounding context (up to 5 lines before/after)
        var lines = sourceCode.Split('\n');
        var contextStart = Math.Max(0, startLine - 6); // -1 for 0-based, -5 for context
        var contextEnd = Math.Min(lines.Length - 1, endLine + 4);

        var contextLines = lines.Skip(contextStart).Take(contextEnd - contextStart + 1);
        return string.Join("\n", contextLines).Trim();
    }

    #endregion
}

