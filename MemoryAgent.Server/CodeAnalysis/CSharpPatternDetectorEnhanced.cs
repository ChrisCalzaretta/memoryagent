using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// COMPREHENSIVE C# pattern detector based on ALL Azure best practices documentation
/// Implements 60+ patterns from Microsoft Azure Architecture Center
/// </summary>
public class CSharpPatternDetectorEnhanced : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";
    private const string AzureApiImplUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-implementation";
    private const string AzureMonitoringUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring";
    private const string AzureAutoScaleUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/auto-scaling";
    private const string AzureBackgroundJobsUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/background-jobs";

    public string GetLanguage() => "C#";

    public List<PatternType> GetSupportedPatternTypes() => Enum.GetValues<PatternType>().ToList();

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // CACHING PATTERNS (7 patterns)
            patterns.AddRange(DetectCacheAsidePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectWriteThroughPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectWriteBehindPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCacheExpirationPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCacheStampedePrevent(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRefreshAheadPattern(root, filePath, context, sourceCode));
            
            // API DESIGN PATTERNS (8 patterns)
            patterns.AddRange(DetectHttpVerbPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPaginationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectFilteringSortingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVersioningPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHateoasPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectContentNegotiationPattern(root, filePath, context, sourceCode));
            
            // API IMPLEMENTATION PATTERNS (5 patterns)
            patterns.AddRange(DetectIdempotencyPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAsyncOperationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPartialResponsePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectBatchOperationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectETagPattern(root, filePath, context, sourceCode));
            
            // BACKGROUND JOBS PATTERNS (3 patterns)
            patterns.AddRange(DetectHostedServicePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMessageQueuePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHangfirePattern(root, filePath, context, sourceCode));
            
            // MONITORING PATTERNS (3 patterns)
            patterns.AddRange(DetectCorrelationIdPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHealthCheckPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTelemetryPattern(root, filePath, context, sourceCode));
            
            // DATA PARTITIONING PATTERNS (2 patterns)
            patterns.AddRange(DetectShardingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVerticalPartitionPattern(root, filePath, context, sourceCode));
            
            // SECURITY PATTERNS (3 patterns)
            patterns.AddRange(DetectAuthPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCorsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRateLimitingPattern(root, filePath, context, sourceCode));
            
            // CONFIGURATION PATTERNS (2 patterns)
            patterns.AddRange(DetectOptionsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectNamedOptionsPattern(root, filePath, context, sourceCode));
            
            // Plus all existing patterns from base detector...
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting enhanced patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

    #region CACHING PATTERNS (Azure Best Practice)

    private List<CodePattern> DetectCacheAsidePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: if (!cache.TryGetValue) { load from source; cache.Set }
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
        
        foreach (var ifStmt in ifStatements)
        {
            var condition = ifStmt.Condition.ToString();
            
            // Check for TryGetValue with negation
            if ((condition.Contains("!") || condition.Contains("not")) && 
                (condition.Contains("TryGetValue") || condition.Contains("TryGet")))
            {
                // Check if statement body has Set/Add to cache
                var bodyText = ifStmt.Statement.ToString();
                if (bodyText.Contains(".Set(") || bodyText.Contains(".Add("))
                {
                    var pattern = CreatePattern(
                        name: "CacheAside_Pattern",
                        type: PatternType.Caching,
                        category: PatternCategory.Performance,
                        implementation: "Cache-Aside",
                        filePath: filePath,
                        node: ifStmt,
                        sourceCode: sourceCode,
                        bestPractice: "Cache-Aside pattern (lazy loading from source on cache miss)",
                        azureUrl: AzureCachingUrl,
                        context: context
                    );
                    
                    pattern.Metadata["cache_pattern"] = "cache-aside";
                    pattern.Metadata["lazy_loading"] = true;
                    pattern.Confidence = 0.95f;
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectWriteThroughPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: await cache.SetAsync(...); await database.UpdateAsync(...);
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var statements = method.DescendantNodes().OfType<ExpressionStatementSyntax>().ToList();
            
            for (int i = 0; i < statements.Count - 1; i++)
            {
                var stmt1 = statements[i].ToString();
                var stmt2 = statements[i + 1].ToString();
                
                // Look for cache set followed by database update
                if ((stmt1.Contains("cache") && stmt1.Contains("Set")) &&
                    (stmt2.Contains("database") || stmt2.Contains("db") || stmt2.Contains("Update") || stmt2.Contains("Save")))
                {
                    var pattern = CreatePattern(
                        name: "WriteThrough_Pattern",
                        type: PatternType.Caching,
                        category: PatternCategory.Performance,
                        implementation: "Write-Through",
                        filePath: filePath,
                        node: statements[i],
                        sourceCode: sourceCode,
                        bestPractice: "Write-Through pattern (synchronous cache and database update)",
                        azureUrl: AzureCachingUrl,
                        context: context
                    );
                    
                    pattern.Metadata["cache_pattern"] = "write-through";
                    pattern.Metadata["consistency"] = "strong";
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectWriteBehindPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: cache.Set + background queue
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            // Look for queue enqueue after cache operations
            if ((invText.Contains("Enqueue") || invText.Contains("QueueBackgroundWorkItem")) &&
                inv.Parent?.ToString().Contains("cache") == true)
            {
                var pattern = CreatePattern(
                    name: "WriteBehind_Pattern",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Write-Behind",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Write-Behind pattern (async database update via queue)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_pattern"] = "write-behind";
                pattern.Metadata["async_persistence"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCacheExpirationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: MemoryCacheEntryOptions with expiration
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(o => o.Type.ToString().Contains("MemoryCacheEntryOptions"));

        foreach (var obj in objectCreations)
        {
            var initText = obj.ToString();
            var hasAbsolute = initText.Contains("AbsoluteExpiration");
            var hasSliding = initText.Contains("SlidingExpiration");
            var hasPriority = initText.Contains("Priority");

            if (hasAbsolute || hasSliding)
            {
                var expirationTypes = new List<string>();
                if (hasAbsolute) expirationTypes.Add("absolute");
                if (hasSliding) expirationTypes.Add("sliding");
                
                var pattern = CreatePattern(
                    name: "CacheExpiration_Policy",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: string.Join("+", expirationTypes),
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: $"Cache expiration policy ({string.Join(" and ", expirationTypes)})",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["absolute_expiration"] = hasAbsolute;
                pattern.Metadata["sliding_expiration"] = hasSliding;
                pattern.Metadata["has_priority"] = hasPriority;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCacheStampedePrevent(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: lock or SemaphoreSlim around cache operations
        var lockStatements = root.DescendantNodes().OfType<LockStatementSyntax>();
        var usingStatements = root.DescendantNodes().OfType<UsingStatementSyntax>();

        foreach (var lockStmt in lockStatements)
        {
            if (lockStmt.Statement.ToString().Contains("cache") || 
                lockStmt.Statement.ToString().Contains("TryGetValue"))
            {
                var pattern = CreatePattern(
                    name: "CacheStampede_Prevention",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Lock",
                    filePath: filePath,
                    node: lockStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Cache stampede prevention using lock",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["concurrency_control"] = "lock";
                patterns.Add(pattern);
            }
        }

        // Check for SemaphoreSlim
        foreach (var usingStmt in usingStatements)
        {
            if (usingStmt.ToString().Contains("SemaphoreSlim") || 
                usingStmt.ToString().Contains("WaitAsync"))
            {
                var pattern = CreatePattern(
                    name: "CacheStampede_SemaphoreSlim",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "SemaphoreSlim",
                    filePath: filePath,
                    node: usingStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Cache stampede prevention using SemaphoreSlim",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["concurrency_control"] = "semaphore";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRefreshAheadPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: proactive cache refresh before expiration
        // Look for: if (timeUntilExpiration < threshold) { refresh }
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
        
        foreach (var ifStmt in ifStatements)
        {
            var condition = ifStmt.Condition.ToString().ToLower();
            var body = ifStmt.Statement.ToString();
            
            if ((condition.Contains("expir") || condition.Contains("ttl") || condition.Contains("threshold")) &&
                (body.Contains("Refresh") || body.Contains("Reload") || body.Contains("Task.Run")))
            {
                var pattern = CreatePattern(
                    name: "RefreshAhead_Pattern",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Refresh-Ahead",
                    filePath: filePath,
                    node: ifStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Refresh-ahead pattern (proactive cache refresh)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_pattern"] = "refresh-ahead";
                pattern.Metadata["proactive"] = true;
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region API DESIGN PATTERNS

    private List<CodePattern> DetectHttpVerbPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var httpVerbAttrs = new[] { "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete", "HttpHead", "HttpOptions" };
        
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            if (httpVerbAttrs.Contains(attrName))
            {
                var pattern = CreatePattern(
                    name: $"HttpVerb_{attrName}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: attrName,
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"Proper HTTP verb usage ({attrName})",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var metadata = GetHttpVerbMetadata(attrName);
                foreach (var kvp in metadata)
                {
                    pattern.Metadata[kvp.Key] = kvp.Value;
                }
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private Dictionary<string, object> GetHttpVerbMetadata(string verb) => verb switch
    {
        "HttpGet" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = true },
        "HttpPost" => new() { ["safe"] = false, ["idempotent"] = false, ["cacheable"] = false },
        "HttpPut" => new() { ["safe"] = false, ["idempotent"] = true, ["cacheable"] = false },
        "HttpPatch" => new() { ["safe"] = false, ["idempotent"] = false, ["cacheable"] = false },
        "HttpDelete" => new() { ["safe"] = false, ["idempotent"] = true, ["cacheable"] = false },
        "HttpHead" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = true },
        "HttpOptions" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = false },
        _ => new()
    };

    private List<CodePattern> DetectPaginationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: class with PageNumber, PageSize, TotalCount properties
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
            var propNames = properties.Select(p => p.Identifier.Text.ToLower()).ToList();
            
            var hasPageNumber = propNames.Any(n => n.Contains("page") && n.Contains("number"));
            var hasPageSize = propNames.Any(n => n.Contains("page") && n.Contains("size"));
            var hasTotalCount = propNames.Any(n => n.Contains("total"));
            
            if (hasPageNumber && hasPageSize && hasTotalCount)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_Pagination",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "Pagination",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Pagination support for large result sets",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var hasNext = propNames.Any(n => n.Contains("next"));
                var hasPrev = propNames.Any(n => n.Contains("prev"));
                
                pattern.Metadata["has_navigation_links"] = hasNext && hasPrev;
                pattern.Metadata["hateoas_compliant"] = hasNext && hasPrev;
                patterns.Add(pattern);
            }
        }

        // Also detect pagination parameters in methods
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var paramNames = parameters.Select(p => p.Identifier.Text.ToLower()).ToList();
            
            if (paramNames.Any(n => n.Contains("page")) && paramNames.Any(n => n.Contains("size")))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_PaginationParams",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "PaginationParameters",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Pagination parameters in API method",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectIdempotencyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [FromHeader(Name = "Idempotency-Key")]
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            foreach (var attr in attrs)
            {
                if (attr.ToString().Contains("FromHeader") && 
                    attr.ToString().Contains("Idempotency"))
                {
                    var pattern = CreatePattern(
                        name: "Idempotency_Key",
                        type: PatternType.ApiDesign,
                        category: PatternCategory.Reliability,
                        implementation: "IdempotencyKey",
                        filePath: filePath,
                        node: param,
                        sourceCode: sourceCode,
                        bestPractice: "Idempotency key for safe retries",
                        azureUrl: AzureApiImplUrl,
                        context: context
                    );
                    
                    pattern.Metadata["prevents_duplicate_operations"] = true;
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAsyncOperationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: return Accepted() with status URL
        var returnStatements = root.DescendantNodes().OfType<ReturnStatementSyntax>();
        
        foreach (var ret in returnStatements)
        {
            if (ret.ToString().Contains("Accepted(") || ret.ToString().Contains("StatusCode(202"))
            {
                var pattern = CreatePattern(
                    name: "LongRunning_AsyncOperation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "AsyncOperation",
                    filePath: filePath,
                    node: ret,
                    sourceCode: sourceCode,
                    bestPractice: "Long-running operation with 202 Accepted response",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["http_status"] = 202;
                pattern.Metadata["async_pattern"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region MONITORING PATTERNS

    private List<CodePattern> DetectCorrelationIdPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: CorrelationId in log scope or method
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("BeginScope") && inv.ToString().Contains("CorrelationId"))
            {
                var pattern = CreatePattern(
                    name: "CorrelationId_Logging",
                    type: PatternType.Monitoring,
                    category: PatternCategory.Operational,
                    implementation: "CorrelationId",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Correlation ID for distributed tracing",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["distributed_tracing"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHealthCheckPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: IHealthCheck implementation
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHealthCheck")) == true)
            {
                var pattern = CreatePattern(
                    name: classDecl.Identifier.Text,
                    type: PatternType.Monitoring,
                    category: PatternCategory.Reliability,
                    implementation: "HealthCheck",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Health check implementation for monitoring",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["health_check"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTelemetryPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: TelemetryClient.Track*
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            if (invText.Contains("TrackEvent") || invText.Contains("TrackMetric") || 
                invText.Contains("TrackDependency") || invText.Contains("TrackException"))
            {
                var trackType = invText.Contains("Event") ? "Event" :
                              invText.Contains("Metric") ? "Metric" :
                              invText.Contains("Dependency") ? "Dependency" : "Exception";
                
                var pattern = CreatePattern(
                    name: $"ApplicationInsights_{trackType}",
                    type: PatternType.Monitoring,
                    category: PatternCategory.Operational,
                    implementation: "ApplicationInsights",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: $"Application Insights telemetry ({trackType})",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["telemetry_type"] = trackType;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region BACKGROUND JOB PATTERNS

    private List<CodePattern> DetectHostedServicePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHostedService")) == true)
            {
                var pattern = CreatePattern(
                    name: classDecl.Identifier.Text,
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: "IHostedService",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Background service using IHostedService",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMessageQueuePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [ServiceBusTrigger], [QueueTrigger]
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("ServiceBusTrigger") || 
                attr.Name.ToString().Contains("QueueTrigger"))
            {
                var pattern = CreatePattern(
                    name: "MessageQueue_Consumer",
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: attr.Name.ToString(),
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "Message queue consumer pattern",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHangfirePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("BackgroundJob.Enqueue") || 
                inv.ToString().Contains("RecurringJob.AddOrUpdate"))
            {
                var pattern = CreatePattern(
                    name: "Hangfire_BackgroundJob",
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: "Hangfire",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Hangfire background job scheduling",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "Hangfire";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Additional API Design & Implementation Patterns

    private List<CodePattern> DetectFilteringSortingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [FromQuery] filter, sort, fields parameters
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var hasFilter = parameters.Any(p => p.Identifier.Text.ToLower() is "filter" or "search" or "query");
            var hasSort = parameters.Any(p => p.Identifier.Text.ToLower() is "sort" or "orderby" or "sortby");
            var hasFields = parameters.Any(p => p.Identifier.Text.ToLower() is "fields" or "select");
            
            if (hasFilter || hasSort || hasFields)
            {
                var features = new List<string>();
                if (hasFilter) features.Add("filtering");
                if (hasSort) features.Add("sorting");
                if (hasFields) features.Add("field selection");
                
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_QueryCapabilities",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: string.Join("+", features),
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: $"API query capabilities: {string.Join(", ", features)}",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["supports_filtering"] = hasFilter;
                pattern.Metadata["supports_sorting"] = hasSort;
                pattern.Metadata["supports_field_selection"] = hasFields;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectVersioningPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: [Route("api/v1/...")] - URI versioning
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString() == "Route")
            {
                var routeArgs = attr.ArgumentList?.Arguments.ToString() ?? "";
                if (Regex.IsMatch(routeArgs, @"v\d+", RegexOptions.IgnoreCase))
                {
                    var pattern = CreatePattern(
                        name: "API_UriVersioning",
                        type: PatternType.ApiDesign,
                        category: PatternCategory.Operational,
                        implementation: "UriVersioning",
                        filePath: filePath,
                        node: attr,
                        sourceCode: sourceCode,
                        bestPractice: "API versioning via URI path",
                        azureUrl: AzureApiDesignUrl,
                        context: context
                    );
                    
                    pattern.Metadata["versioning_strategy"] = "uri";
                    patterns.Add(pattern);
                }
            }
            
            // Pattern 2: [ApiVersion("1.0")] - Header/Query versioning
            if (attr.Name.ToString().Contains("ApiVersion"))
            {
                var pattern = CreatePattern(
                    name: "API_HeaderVersioning",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "ApiVersionAttribute",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "API versioning using ApiVersion attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["versioning_strategy"] = "attribute";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHateoasPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Classes with "Links" property or HATEOAS response structure
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            var hasLinks = properties.Any(p => p.Identifier.Text is "Links" or "_links" or "Href");
            
            if (hasLinks)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_HATEOAS",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "HATEOAS",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "HATEOAS (Hypermedia as the Engine of Application State)",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["hypermedia_support"] = true;
                pattern.Metadata["api_discoverability"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectContentNegotiationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [Produces(...)] or [Consumes(...)] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            if (attrName is "Produces" or "Consumes")
            {
                var pattern = CreatePattern(
                    name: $"{attrName}_ContentNegotiation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: attrName,
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"Content negotiation using {attrName} attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var args = attr.ArgumentList?.Arguments.ToString() ?? "";
                pattern.Metadata["content_types"] = args;
                pattern.Metadata["supports_multiple_formats"] = args.Contains(",");
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPartialResponsePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Field selection / projection logic
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var bodyText = method.Body?.ToString() ?? "";
            
            // Look for Select(), projection, or field filtering
            if ((bodyText.Contains("Select(") && bodyText.Contains("fields")) ||
                bodyText.Contains("SelectFields") ||
                bodyText.Contains("ProjectTo") ||
                (bodyText.Contains("fields") && bodyText.Contains("Split")))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_PartialResponse",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "SparseFieldsets",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Partial response / sparse fieldsets for bandwidth optimization",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["reduces_payload_size"] = true;
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectBatchOperationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Method accepting List<T> or IEnumerable<T> for batch operations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var hasBatchParam = parameters.Any(p => 
                p.Type?.ToString().Contains("List<") == true || 
                p.Type?.ToString().Contains("IEnumerable<") == true ||
                p.Type?.ToString().Contains("[]") == true);
            
            var methodName = method.Identifier.Text.ToLower();
            var isBatchMethod = methodName.Contains("batch") || methodName.Contains("bulk") || methodName.Contains("multiple");
            
            if (hasBatchParam && (isBatchMethod || parameters.Count == 1))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_BatchOperation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "BatchAPI",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Batch operation API for reduced round-trips",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["reduces_round_trips"] = true;
                pattern.Confidence = isBatchMethod ? 1.0f : 0.7f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectETagPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Setting ETag header
        var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var assignment in assignments)
        {
            if (assignment.ToString().Contains("ETag") || assignment.ToString().Contains("\"ETag\""))
            {
                var pattern = CreatePattern(
                    name: "ETag_Generation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "ETag",
                    filePath: filePath,
                    node: assignment,
                    sourceCode: sourceCode,
                    bestPractice: "ETag header for HTTP caching and concurrency control",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["supports_caching"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: If-Match / If-None-Match header validation
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            if (attrs.Any(a => a.ToString().Contains("If-Match") || a.ToString().Contains("If-None-Match")))
            {
                var pattern = CreatePattern(
                    name: "ETag_Validation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Reliability,
                    implementation: "ETagValidation",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: "ETag validation for optimistic concurrency control",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["prevents_lost_updates"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectShardingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Shard key calculation / routing logic
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text.ToLower();
            var bodyText = method.Body?.ToString() ?? "";
            
            // Look for shard key calculation
            if ((methodName.Contains("shard") || methodName.Contains("partition")) ||
                (bodyText.Contains("% ") && (bodyText.Contains("shard") || bodyText.Contains("partition"))) ||
                bodyText.Contains("GetShard") ||
                bodyText.Contains("ShardKey"))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_Sharding",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "HorizontalPartitioning",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Horizontal partitioning (sharding) for data distribution",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["scalability_pattern"] = true;
                pattern.Metadata["partition_type"] = "horizontal";
                pattern.Confidence = methodName.Contains("shard") ? 1.0f : 0.7f;
                patterns.Add(pattern);
            }
        }

        // Also check for Dictionary/Map of multiple data stores
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            if ((fieldType.Contains("Dictionary") || fieldType.Contains("IDictionary")) &&
                (fieldType.Contains("Repository") || fieldType.Contains("DataSource") || fieldType.Contains("Connection")))
            {
                var pattern = CreatePattern(
                    name: "MultiShard_DataStore",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "ShardMap",
                    filePath: filePath,
                    node: field,
                    sourceCode: sourceCode,
                    bestPractice: "Multiple data store instances for sharding",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["shard_map"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectVerticalPartitionPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Separate classes/repositories for hot vs cold data
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        var hotDataClasses = classes.Where(c => c.Identifier.Text.Contains("Profile") || 
                                                c.Identifier.Text.Contains("Core") ||
                                                c.Identifier.Text.Contains("Active")).ToList();
        var coldDataClasses = classes.Where(c => c.Identifier.Text.Contains("History") || 
                                                 c.Identifier.Text.Contains("Archive") ||
                                                 c.Identifier.Text.Contains("Audit")).ToList();
        
        foreach (var coldClass in coldDataClasses)
        {
            // Check if there's a corresponding hot class
            var baseName = coldClass.Identifier.Text.Replace("History", "").Replace("Archive", "").Replace("Audit", "");
            if (hotDataClasses.Any(h => h.Identifier.Text.Contains(baseName)))
            {
                var pattern = CreatePattern(
                    name: $"{coldClass.Identifier.Text}_VerticalPartition",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "VerticalPartitioning",
                    filePath: filePath,
                    node: coldClass,
                    sourceCode: sourceCode,
                    bestPractice: "Vertical partitioning (hot/cold data separation)",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["partition_type"] = "vertical";
                pattern.Metadata["data_temperature"] = "cold";
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAuthPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [Authorize] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("Authorize"))
            {
                var args = attr.ArgumentList?.Arguments.ToString() ?? "";
                var hasPolicy = args.Contains("Policy");
                var hasRoles = args.Contains("Roles");
                var hasScheme = args.Contains("AuthenticationSchemes");
                
                var authType = hasPolicy ? "Policy-Based" : hasRoles ? "Role-Based" : hasScheme ? "Scheme-Based" : "Simple";
                
                var pattern = CreatePattern(
                    name: $"Authorization_{authType}",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: $"Authorize_{authType}",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"{authType} authorization",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["auth_type"] = authType.ToLower();
                pattern.Metadata["has_policy"] = hasPolicy;
                pattern.Metadata["has_roles"] = hasRoles;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCorsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AddCors configuration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("AddCors") || inv.ToString().Contains("UseCors"))
            {
                var pattern = CreatePattern(
                    name: "CORS_Configuration",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "CORS",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "CORS (Cross-Origin Resource Sharing) configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var invText = inv.ToString();
                pattern.Metadata["with_origins"] = invText.Contains("WithOrigins");
                pattern.Metadata["allow_credentials"] = invText.Contains("AllowCredentials");
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRateLimitingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: [EnableRateLimiting] or [RateLimit] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("RateLimit") || attr.Name.ToString().Contains("Throttle"))
            {
                var pattern = CreatePattern(
                    name: "RateLimit_Attribute",
                    type: PatternType.Security,
                    category: PatternCategory.Reliability,
                    implementation: "RateLimiting",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "Rate limiting for API throttling",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["protects_from_abuse"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: AddRateLimiter service registration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("AddRateLimiter"))
            {
                var pattern = CreatePattern(
                    name: "RateLimit_Service",
                    type: PatternType.Security,
                    category: PatternCategory.Reliability,
                    implementation: "RateLimiterService",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Rate limiter service configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOptionsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: IOptions<T> injection
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        
        foreach (var param in parameters)
        {
            var paramType = param.Type?.ToString() ?? "";
            if (paramType.Contains("IOptions<") || paramType.Contains("IOptionsSnapshot<") || paramType.Contains("IOptionsMonitor<"))
            {
                var optionsType = paramType.Contains("IOptionsSnapshot") ? "Snapshot" :
                                paramType.Contains("IOptionsMonitor") ? "Monitor" : "Standard";
                
                var pattern = CreatePattern(
                    name: $"Options_{optionsType}",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: $"IOptions{(optionsType != "Standard" ? optionsType : "")}",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: $"Options pattern ({optionsType}) for strongly-typed configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["options_type"] = optionsType.ToLower();
                pattern.Metadata["strongly_typed_config"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Configure<T> service registration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("Configure<"))
            {
                var pattern = CreatePattern(
                    name: "Options_Configuration",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: "ConfigureOptions",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Options configuration registration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNamedOptionsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Configure<T>("name", ...) - named options
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("Configure<") && inv.ArgumentList != null)
            {
                var args = inv.ArgumentList.Arguments;
                // Check if first argument is a string literal (name)
                if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax literal && 
                    literal.Kind() == SyntaxKind.StringLiteralExpression)
                {
                    var pattern = CreatePattern(
                        name: "NamedOptions_Configuration",
                        type: PatternType.Configuration,
                        category: PatternCategory.Operational,
                        implementation: "NamedOptions",
                        filePath: filePath,
                        node: inv,
                        sourceCode: sourceCode,
                        bestPractice: "Named options for multiple configurations",
                        azureUrl: AzureApiDesignUrl,
                        context: context
                    );
                    
                    pattern.Metadata["supports_multiple_configs"] = true;
                    patterns.Add(pattern);
                }
            }
        }

        // Also detect IOptionsSnapshot.Get("name")
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains(".Get(") && inv.ToString().Contains("options"))
            {
                var pattern = CreatePattern(
                    name: "NamedOptions_Retrieval",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: "NamedOptionsRetrieval",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Named options retrieval",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Confidence = 0.7f;
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
        SyntaxNode node,
        string sourceCode,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var endLineNumber = lineSpan.EndLinePosition.Line + 1;
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
        var lines = sourceCode.Split('\n');
        var contextStart = Math.Max(0, startLine - 6);
        var contextEnd = Math.Min(lines.Length - 1, endLine + 4);
        var contextLines = lines.Skip(contextStart).Take(contextEnd - contextStart + 1);
        return string.Join("\n", contextLines).Trim();
    }

    #endregion
}

